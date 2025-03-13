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
    
    // 文本滚动索引
    private int _userTextScrollIndex = 0;
    private int _aiTextScrollIndex = 0;
    
    // 滚动文本长度限制
    private const int MaxScrollTextLength = 100;
    
    // Skia相关对象
    private SKTypeface _typeface;
    private SKBitmap _bitmapImage;

    public LcdService(ILogger<LcdService> logger, IOptions<LcdSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
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

    private void InitializeDisplay()
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
        _bitmapImage = new SKBitmap(_settings.Width, _settings.Height, true);

        // 显示欢迎消息
        _display.ClearScreen(Color.Black, true);
        DrawWelcomeScreen();
        
        // 设置背光
        _display.SetBacklight(80);
    }
    
    private void DrawWelcomeScreen()
    {
        using (var canvas = new SKCanvas(_bitmapImage))
        {
            // 清空画布
            canvas.Clear(SKColors.Black);
            
            // 创建画笔
            using (var paint = new SKPaint())
            {
                paint.Color = SKColors.White;
                paint.TextSize = 24;
                paint.IsAntialias = true;
                paint.Typeface = _typeface;
                
                canvas.DrawText("欢迎使用AI助手", 20, 40, paint);
                
                paint.Color = SKColors.Cyan;
                paint.TextSize = 20;
                canvas.DrawText("等待对话开始...", 20, 80, paint);
            }
        }
        
        // 将绘制的内容显示到屏幕
        _display.DrawBitmap(BitmapImage.CreateFromStream(new MemoryStream(_bitmapImage.Encode(SKEncodedImageFormat.Png, 100).ToArray())) , new Point(0,0), new Rectangle(0, 0, _settings.Width, _settings.Height), true);
    }

    private async void StartDisplayUpdateLoop()
    {
        _isUpdating = true;
        
        try
        {
            while (!_displayUpdateCts.Token.IsCancellationRequested)
            {
                await UpdateDisplayAsync();
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

    public async Task UpdateUserTextAsync(string text)
    {
        if (!_isInitialized) return;
        
        await _semaphore.WaitAsync();
        try
        {
            _currentUserText = text;
            // 重置滚动索引
            _userTextScrollIndex = 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateAiTextAsync(string text)
    {
        if (!_isInitialized) return;
        
        await _semaphore.WaitAsync();
        try
        {
            _currentAiText = text;
            // 重置滚动索引
            _aiTextScrollIndex = 0;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task UpdateDisplayAsync()
    {
        if (!_isInitialized) return;
        
        await _semaphore.WaitAsync();
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
                    
                    // 绘制标题和分隔线
                    paint.Color = SKColors.Yellow;
                    paint.TextSize = 20;
                    canvas.DrawText("用户:", 10, 25, paint);
                    
                    // 绘制分隔线
                    paint.Color = SKColors.Gray;
                    paint.StrokeWidth = 1;
                    canvas.DrawLine(5, 35, _settings.Width - 5, 35, paint);
                    
                    // 处理用户文本
                    string userDisplayText = _currentUserText;
                    if (userDisplayText.Length > MaxScrollTextLength)
                    {
                        userDisplayText = userDisplayText.Substring(0, MaxScrollTextLength) + "...";
                    }
                    
                    // 用户文本换行显示
                    paint.Color = SKColors.White;
                    paint.TextSize = 16;
                    WrapAndDrawText(canvas, paint, userDisplayText, 10, 55);
                    
                    // 绘制AI回复标题和分隔线
                    paint.Color = SKColors.Green;
                    paint.TextSize = 20;
                    canvas.DrawText("AI:", 10, 135, paint);
                    
                    // 绘制分隔线
                    paint.Color = SKColors.Gray;
                    paint.StrokeWidth = 1;
                    canvas.DrawLine(5, 145, _settings.Width - 5, 145, paint);
                    
                    // 处理AI文本
                    string aiDisplayText = _currentAiText;
                    if (aiDisplayText.Length > MaxScrollTextLength)
                    {
                        aiDisplayText = aiDisplayText.Substring(0, MaxScrollTextLength) + "...";
                    }
                    
                    // AI文本换行显示
                    paint.Color = SKColors.Cyan;
                    paint.TextSize = 16;
                    WrapAndDrawText(canvas, paint, aiDisplayText, 10, 165);
                }
            }
            // 更新显示
            _display.DrawBitmap(BitmapImage.CreateFromStream(new MemoryStream(_bitmapImage.Encode(SKEncodedImageFormat.Png, 100).ToArray())) , new Point(0,0), new Rectangle(0, 0, _settings.Width, _settings.Height), true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新NV3030B显示时出错");
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    // 文本换行显示
    private void WrapAndDrawText(SKCanvas canvas, SKPaint paint, string text, int x, int y)
    {
        if (string.IsNullOrEmpty(text))
            return;
            
        float fontSize = paint.TextSize;
        int charsPerLine = (int)((_settings.Width - x * 2) / (fontSize / 2)); // 根据字体大小计算每行字符数
        float yPos = y;
        
        for (int i = 0; i < text.Length; i += charsPerLine)
        {
            string line = i + charsPerLine >= text.Length 
                ? text.Substring(i) 
                : text.Substring(i, charsPerLine);
                
            canvas.DrawText(line, x, yPos, paint);
            yPos += fontSize + 4; // 行间距
            
            // 防止绘制超出屏幕
            if (yPos > _settings.Height - fontSize)
                break;
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
