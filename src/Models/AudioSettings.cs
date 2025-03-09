public class AudioSettings
{
    public string MixerDeviceName { get; set; }
    public string PlaybackDeviceName { get; set; }
    public string RecordingDeviceName { get; set; }
    public uint RecordingSampleRate { get; set; } = 16_000;
    public ushort RecordingBitsPerSample { get; set; } = 16;
}