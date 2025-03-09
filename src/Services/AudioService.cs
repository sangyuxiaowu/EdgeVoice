using Alsa.Net;

public class AudioService
{
    private readonly SoundDeviceSettings _settings;

    public AudioService()
    {
        _settings = new SoundDeviceSettings()
        {
            MixerDeviceName = "hw:0", // 混音设备名称
            PlaybackDeviceName = "hw:0", // 播放设备名称
            RecordingDeviceName = "hw:0", // 录音设备名称
            RecordingSampleRate = 16_000
        };
    }

    public void RecordAudio(string filePath, uint duration)
    {
        using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
        Console.WriteLine("Recording...");
        alsaDevice.Record(duration, filePath);
    }

    public void PlayAudio(string filePath)
    {
        using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
        Console.WriteLine("Playing...");
        alsaDevice.Play(filePath);
    }
}
