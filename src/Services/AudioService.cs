using Alsa.Net;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using static AudioUtils;

public class AudioService : IDisposable
{
    private readonly SoundDeviceSettings _settings;
    private CancellationTokenSource _cancellationTokenSourceRecording;

    private CancellationTokenSource _cancellationTokenSourcePlayback = new CancellationTokenSource();

    public event Func<byte[], Task> OnAudioDataAvailable;
    public event Action OnRecordingStopped;

    /// <summary>
    /// 是否已经丢弃首包的WAV音频头
    /// </summary>
    private bool hasDiscardedWavHeader = false;

    private readonly Queue<byte[]> _packetQueue = new Queue<byte[]>();
    private const int PacketThreshold = 30;
    private const int PacketSize = 256;
    private readonly ConcurrentQueue<byte[]> _playbackQueue = new ConcurrentQueue<byte[]>();
    private bool _isPlaying = false;

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
            //RecordingChannels = 1, // 录音通道数  单通道录音Alsa.Net会报错
        };
    }

    public async Task StartRecordingAsync()
    {
        _cancellationTokenSourceRecording = new CancellationTokenSource();
        using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
        await Task.Run(() => alsaDevice.Record(async (data) => 
        {
            if (OnAudioDataAvailable != null)
            {
                
                if (!hasDiscardedWavHeader)
                {
                    hasDiscardedWavHeader = true;
                    return;
                }
                // 将数据改为单声道
                data = ConvertStereoToMono(data);
                // data 512 转换后为 256 字节

                _packetQueue.Enqueue(data);

                if (_packetQueue.Count >= PacketThreshold)
                {
                    await SendPackets();
                }
                
            }
        }, _cancellationTokenSourceRecording.Token), _cancellationTokenSourceRecording.Token);
    }

    private async Task SendPackets()
    {
        int totalSize = PacketThreshold * PacketSize;
        byte[] combinedPacket = new byte[totalSize];
        int offset = 0;

        while (_packetQueue.Count > 0)
        {
            byte[] packet = _packetQueue.Dequeue();
            Buffer.BlockCopy(packet, 0, combinedPacket, offset, PacketSize);
            offset += PacketSize;
        }

        await OnAudioDataAvailable(combinedPacket);
    }

    public async Task PlayAudioAsync(byte[] pcmData)
    {
        var monoData = ConvertMonoToStereo(pcmData);
        _playbackQueue.Enqueue(monoData);

        if (!_isPlaying)
        {
            _isPlaying = true;
            await Task.Run(() => ProcessPlaybackQueue());
        }
    }

    private void ProcessPlaybackQueue()
    {
        while (_playbackQueue.TryDequeue(out var pcmData))
        {
            Play(pcmData);
        }
        _isPlaying = false;
    }

    private void Play(byte[] pcmData)
    {
        // 创建 WAV 头
        byte[] wavHeader = CreateWavHeader(22050, 16, 2);
        // 组合 WAV 头和 PCM 数据
        byte[] wavData = new byte[wavHeader.Length + pcmData.Length];
        wavHeader.CopyTo(wavData, 0);
        pcmData.CopyTo(wavData, wavHeader.Length);
        using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
        alsaDevice.Play(new MemoryStream(wavData), _cancellationTokenSourcePlayback.Token);
    }

    private void StopPlayback()
    {
        _cancellationTokenSourcePlayback?.Cancel();
        _cancellationTokenSourcePlayback = new CancellationTokenSource();
        _isPlaying = false;
        _playbackQueue.Clear();
    }

    public void StopRecording()
    {
        hasDiscardedWavHeader = false;
        _cancellationTokenSourceRecording?.Cancel();
        OnRecordingStopped?.Invoke();
    }

    public void Dispose()
    {
        _cancellationTokenSourceRecording?.Cancel();
        _cancellationTokenSourcePlayback?.Cancel();
    }
}
