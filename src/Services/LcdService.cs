using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sang.IoT.NV3030B;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Device.Spi;
using System.Drawing;
using Iot.Device.Graphics.SkiaSharpAdapter;
using SkiaSharp;
using Iot.Device.Graphics;
using System.Threading.Tasks;

public class LcdService : IDisposable
{
    private readonly ILogger<LcdService> _logger;
    private readonly LcdSettings _settings;
    private NV3030B _display;
    private bool _isInitialized = false;
    private string _currentUserText = "";
    private string _currentAiText = "";
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private CancellationTokenSource _displayUpdateCts;
    private bool _isUpdating = false;
    
    // 文本长度限制
    private const int MaxUserTextLength = 50;
    private const int MaxAITextLength = 100;
    
    // 圆角安全距离
    private const int CornerSafetyMargin = 15;
    
    // Skia相关对象
    private SKTypeface _typeface;
    private SKBitmap _bitmapImage;

    // 增加内容变化标志
    private bool _contentChanged = false;
    private string _lastUserText = "";
    private string _lastAiText = "";

    public LcdService(ILogger<LcdService> logger, IOptions<LcdSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;

        if(_settings == null || _settings.SpiBus == -1  || _settings.SpiChip == -1)
        {
            _logger.LogWarning("未配置显示器，将禁用显示功能");
            return;
        }

        _displayUpdateCts = new CancellationTokenSource();
        
        try
        {
            // 注册SkiaSharp适配器
            SkiaSharpAdapter.Register();
            
            // 加载字体
            if (!File.Exists(_settings.FontPath))
            {
                _logger.LogWarning($"字体文件不存在: {_settings.FontPath}，将使用默认字体");
                _typeface = SKTypeface.Default;
            }
            else
            {
                _typeface = SKTypeface.FromFile(_settings.FontPath);
                _logger.LogInformation($"已加载字体: {_settings.FontPath}");
            }
            
            InitializeDisplay();
            _isInitialized = true;
            StartDisplayUpdateLoop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化NV3030B显示器失败");
            _isInitialized = false;
        }
    }

    // 初始化显示器
    private async Task InitializeDisplay()
    {
        _logger.LogInformation($"初始化NV3030B显示器 (SPI总线: {_settings.SpiBus}, 芯片: {_settings.SpiChip})");
        
        // 创建SPI配置
        var spiConnectionSettings = new SpiConnectionSettings(_settings.SpiBus, _settings.SpiChip)
        {
            ClockFrequency = 40_000_000, // 40MHz
            Mode = SpiMode.Mode0,
            DataBitLength = 8
        };
        
        var spiDevice = SpiDevice.Create(spiConnectionSettings);

        // 根据配置创建显示器实例
        if (_settings.UseSysFsDriver)
        {
            // 使用SysFsDriver (适用于某些设备如LuckFox)
            var gpioController = new GpioController(PinNumberingScheme.Logical, new SysFsDriver());
            _display = new NV3030B(spiDevice, _settings.DCPin, _settings.ResetPin, _settings.BacklightPin, gpioController: gpioController);
        }
        else
        {
            // 使用默认GPIO驱动
            _display = new NV3030B(spiDevice, _settings.DCPin, _settings.ResetPin, _settings.BacklightPin);
        }

        // 初始化位图
        _bitmapImage = new SKBitmap(_settings.Width, _settings.Height, false);

        // 设置背光
        //_display.SetBacklight(80);
        _display.ClearScreen(Color.Black, false);
        _display.SendFrame(true);
    }

    public void UpdateStatus(string text = "聆听中", bool withImg = true)
    {
        if (!_isInitialized) return;

        if (withImg){
            _display.DrawBitmap(BitmapImage.CreateFromFile("img/p1.png") , new Point(0,0), new Rectangle(0, 0, _settings.Width, _settings.Height), false);
        }

        using(var textimg = new SKBitmap(_settings.Width, 80)){
            using(var canvas = new SKCanvas(textimg)){
                canvas.Clear(SKColors.Black);
                using (var paint = new SKPaint())
                {
                    paint.IsAntialias = true;
                    paint.Typeface = _typeface;
                    paint.Color = SKColors.White;
                    paint.TextSize = 50;
                    canvas.DrawText(text, 40, 60, paint);
                }
            }
            _display.DrawBitmap(BitmapImage.CreateFromStream(new MemoryStream(textimg.Encode(SKEncodedImageFormat.Jpeg, 80).ToArray())) , new Point(0,0), new Rectangle(0, 0, _settings.Width, 80), false);
        }
        _display.SendFrame(true);
    }
    
    private async void StartDisplayUpdateLoop()
    {
        _isUpdating = true;
        
        try
        {
            while (!_displayUpdateCts.Token.IsCancellationRequested)
            {

                await _semaphore.WaitAsync();
                try 
                {
                    // 只有当内容变化时才更新显示
                    if (_contentChanged)
                    {
                        await UpdateDisplayAsync();
                        _contentChanged = false;
                        
                        // 更新最后显示的内容
                        _lastUserText = _currentUserText;
                        _lastAiText = _currentAiText;
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
                
                await Task.Delay(_settings.RefreshDelay, _displayUpdateCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NV3030B显示更新循环出错");
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// 更新用户文本
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public async Task UpdateUserTextAsync(string text)
    {
        if (!_isInitialized) return;
        
        await _semaphore.WaitAsync();
        try
        {
            // 检查内容是否发生变化
            if (_currentUserText != text)
            {
                _currentUserText = text;
                // 标记内容已变化
                _contentChanged = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 更新AI文本
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public async Task UpdateAiTextAsync(string text)
    {
        if (!_isInitialized) return;
        
        await _semaphore.WaitAsync();
        try
        {
            // 检查内容是否发生变化
            if (_currentAiText != text)
            {
                _currentAiText = text;
                // 标记内容已变化
                _contentChanged = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task UpdateDisplayAsync()
    {
        if (!_isInitialized) return;
        
        try
        {
            using (var canvas = new SKCanvas(_bitmapImage))
            {
                // 清空画布
                canvas.Clear(SKColors.Black);
                
                using (var paint = new SKPaint())
                {
                    paint.IsAntialias = true;
                    paint.Typeface = _typeface;
                    
                    // 考虑圆角安全距离调整标题和分隔线位置
                    paint.Color = SKColors.Yellow;
                    paint.TextSize = 20;
                    canvas.DrawText("用户:", 50, 25 , paint);
                    
                    // 绘制分隔线，考虑圆角
                    paint.Color = SKColors.Gray;
                    paint.StrokeWidth = 1;
                    canvas.DrawLine(
                        CornerSafetyMargin, 35, 
                        _settings.Width - CornerSafetyMargin, 35, 
                        paint);
                    
                    // 处理用户文本
                    string userDisplayText = _currentUserText;
                    if (userDisplayText.Length > MaxUserTextLength)
                    {
                        userDisplayText = userDisplayText.Substring(0, MaxUserTextLength) + "...";
                    }
                    
                    // 用户文本换行显示，考虑圆角
                    paint.Color = SKColors.White;
                    paint.TextSize = 18;
                    WrapAndDrawText(canvas, paint, userDisplayText.Trim(), CornerSafetyMargin, 55);
                    
                    // 绘制AI回复标题和分隔线，考虑圆角
                    paint.Color = SKColors.Green;
                    paint.TextSize = 20;
                    canvas.DrawText("AI:", 50, 125, paint);
                    
                    // 绘制分隔线，考虑圆角
                    paint.Color = SKColors.Gray;
                    paint.StrokeWidth = 1;
                    canvas.DrawLine(
                        CornerSafetyMargin, 135, 
                        _settings.Width - CornerSafetyMargin, 135, 
                        paint);
                    
                    // 处理AI文本
                    string aiDisplayText = _currentAiText;
                    if (aiDisplayText.Length > MaxAITextLength)
                    {
                        aiDisplayText = aiDisplayText.Substring(0, MaxAITextLength) + "...";
                    }
                    
                    // AI文本换行显示，考虑圆角
                    paint.Color = SKColors.Cyan;
                    paint.TextSize = 18;
                    WrapAndDrawText(canvas, paint, aiDisplayText, CornerSafetyMargin, 155);
                }
            }
            var memoryStream = new MemoryStream(_bitmapImage.Encode(SKEncodedImageFormat.Jpeg, 80).ToArray());
            // 更新显示
            _display.DrawBitmap(BitmapImage.CreateFromStream(memoryStream) , new Point(0,0), new Rectangle(0, 0, _settings.Width, _settings.Height), true);
            
            _logger.LogDebug("显示已更新");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新NV3030B显示时出错");
        }
    }
    
    // 文本换行显示，考虑圆角安全距离
    private void WrapAndDrawText(SKCanvas canvas, SKPaint paint, string text, int x, int y)
    {
        if (string.IsNullOrEmpty(text))
            return;
            
        float fontSize = paint.TextSize;
        // 考虑两边的安全距离，一边够了
        int effectiveWidth = _settings.Width - CornerSafetyMargin;
        int charsPerLine = (int)((effectiveWidth - x) / fontSize ); // 根据字体大小计算每行字符数
        float yPos = y;
        
        for (int i = 0; i < text.Length; i += charsPerLine)
        {
            string line = i + charsPerLine >= text.Length 
                ? text.Substring(i) 
                : text.Substring(i, charsPerLine);
                
            canvas.DrawText(line, x, yPos, paint);
            yPos += fontSize + 4; // 行间距
            
            // 防止绘制超出屏幕底部安全区域
            if (yPos > _settings.Height - fontSize)
                break;
        }
    }

    // 增加手动触发刷新的方法，用于必要时强制刷新
    public async Task ForceRefreshAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _contentChanged = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        try
        {
            _displayUpdateCts?.Cancel();
            
            if (_isInitialized)
            {
                _display?.ClearScreen(Color.Black, true);
                _display?.Dispose();
                _typeface?.Dispose();
                _bitmapImage?.Dispose();
            }
            
            _semaphore?.Dispose();
            _displayUpdateCts?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放NV3030B显示器资源时出错");
        }
    }
}
