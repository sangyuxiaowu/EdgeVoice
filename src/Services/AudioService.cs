using Alsa.Net;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

public class AudioService : IDisposable
{
    private readonly SoundDeviceSettings _settings;
    private CancellationTokenSource _cancellationTokenSource;

    public event Func<byte[], Task> OnAudioDataAvailable;
    public event Action OnRecordingStopped;

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

    public async Task StartRecordingAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
        await Task.Run(() => alsaDevice.Record(async (data) => 
        {
            if (OnAudioDataAvailable != null)
            {
                await Task.Run(() => OnAudioDataAvailable(data));
            }
        }, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
    }

    public void StopRecording()
    {
        _cancellationTokenSource?.Cancel();
        OnRecordingStopped?.Invoke();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
    }
}
