using Alsa.Net;
using Microsoft.Extensions.Options;

public class AudioService
{
    private readonly SoundDeviceSettings _settings;

    public AudioService(IOptions<AudioSettings> audioSettings)
    {
        var settings = audioSettings.Value;
        _settings = new SoundDeviceSettings()
        {
            MixerDeviceName = settings.MixerDeviceName, // 混音设备名称
            PlaybackDeviceName = settings.PlaybackDeviceName, // 播放设备名称
            RecordingDeviceName = settings.RecordingDeviceName, // 录音设备名称
            RecordingSampleRate = settings.RecordingSampleRate, // 录音采样率
            RecordingBitsPerSample = settings.RecordingBitsPerSample, // 录音采样位数
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
