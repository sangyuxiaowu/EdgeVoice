using Alsa.Net;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using static AudioUtils;

public class AudioService : IDisposable
{
    /// <summary>
    /// 音频设备设置
    /// </summary>
    private readonly SoundDeviceSettings _settings;
    
    /// <summary>
    /// 录音取消令牌源
    /// </summary>
    private CancellationTokenSource? _cancellationTokenSourceRecording;

    /// <summary>
    /// 播放取消令牌源
    /// </summary>
    private CancellationTokenSource _cancellationTokenSourcePlayback = new CancellationTokenSource();

    /// <summary>
    /// 音频数据可用事件
    /// </summary>
    public event Func<byte[], Task>? OnAudioDataAvailable;
    
    /// <summary>
    /// 录音停止事件
    /// </summary>
    public event Action? OnRecordingStopped;

    /// <summary>
    /// 是否已经丢弃首包的WAV音频头
    /// </summary>
    private bool hasDiscardedWavHeader = false;

    /// <summary>
    /// 音频数据包队列
    /// </summary>
    private readonly Queue<byte[]> _packetQueue = new Queue<byte[]>();
    private const int PacketThreshold = 30;
    private const int PacketSize = 256;
    
    /// <summary>
    /// 播放队列
    /// </summary>
    private readonly ConcurrentQueue<byte[]> _playbackQueue = new ConcurrentQueue<byte[]>();
    private bool _isPlaying = false;

    /// <summary>
    /// Alsa音频设备
    /// </summary>
    private ISoundDevice _alsaDevice;

    /// <summary>
    /// 音频服务
    /// </summary>
    /// <param name="audioSettings"></param>
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
        _alsaDevice = AlsaDeviceBuilder.Create(_settings);
    }

    public async Task StartRecordingAsync()
    {
        _cancellationTokenSourceRecording = new CancellationTokenSource();
        //using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
        await Task.Run(() => _alsaDevice.Record(async (data) => 
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

        if (OnAudioDataAvailable != null){
            await OnAudioDataAvailable(combinedPacket);
        }
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
        var wavData = CreateWavHeader(22050, 16, 2).Concat(pcmData).ToArray();
        //using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
        _alsaDevice.Play(new MemoryStream(wavData), _cancellationTokenSourcePlayback.Token);
    }

    private void StopPlayback()
    {
        _cancellationTokenSourcePlayback?.Cancel();
        _cancellationTokenSourcePlayback = new CancellationTokenSource();
        _isPlaying = false;
        _playbackQueue.Clear();
    }

    #region 音频文件保存和播放

    public async Task SaveAudio(string audioId, byte[] pcmData)
    {
        string filePath = $"audio/{audioId}.wav";
        var monoData = ConvertMonoToStereo(pcmData);
        byte[] wavData;
        if (!File.Exists(filePath))
        {
            byte[] wavHeader = CreateWavHeader(22050, 16, 2);
            wavData = wavHeader.Concat(monoData).ToArray();
            await File.WriteAllBytesAsync(filePath, wavData);
        }
        else
        {
            using (var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write))
            {
                await fileStream.WriteAsync(monoData, 0, monoData.Length);
            }
        }
    }

    public async Task PlayAudio(string audioId)
    {
        string filePath = $"audio/{audioId}.wav";
        if (!File.Exists(filePath))
        {
            return;
        }
        await Task.Run(() =>{
            //using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
            _alsaDevice.Play(filePath, _cancellationTokenSourcePlayback.Token);
            OnPlaybackStopped?.Invoke();
        }, _cancellationTokenSourcePlayback.Token); 
    }

    #endregion

    public void Dispose()
    {
        _cancellationTokenSourceRecording?.Cancel();
        _cancellationTokenSourcePlayback?.Cancel();
        _alsaDevice?.Dispose();
    }
}
