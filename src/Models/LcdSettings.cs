public class LcdSettings
{
    public int SpiBus { get; set; } = 0;
    public int SpiChip { get; set; } = 0;
    public int Width { get; set; } = 240;
    public int Height { get; set; } = 280;
    public int DCPin { get; set; } = 27;
    public int ResetPin { get; set; } = 17;
    public int BacklightPin { get; set; } = 22;
    public int RefreshDelay { get; set; } = 300; // 刷新延迟(毫秒)
    public bool UseSysFsDriver { get; set; } = false; // 是否使用SysFsDriver
    public string FontPath { get; set; } = "/usr/share/fonts/truetype/wqy/wqy-microhei.ttc"; // 字体文件路径
}