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

    /// <summary>
    /// 是否已经丢弃首包的WAV音频头
    /// </summary>
    private bool hasDiscardedWavHeader = false;

    private readonly Queue<byte[]> _packetQueue = new Queue<byte[]>();
    private const int PacketThreshold = 30;
    private const int PacketSize = 256;

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
        _cancellationTokenSource = new CancellationTokenSource();
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
        }, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
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

    private byte[] ConvertStereoToMono(byte[] stereoData)
    {
        // 确保数据长度是4的倍数（双声道16位样本）
        if (stereoData.Length % 4 != 0)
        {
            throw new ArgumentException("输入数据长度必须是4的倍数");
        }

        // 单声道数据长度是双声道的一半（每样本2字节）
        byte[] monoData = new byte[stereoData.Length / 2];
        
        for (int i = 0; i < stereoData.Length; i += 4)
        {
            // 提取左声道样本（16位有符号整数）
            short left = BitConverter.ToInt16(stereoData, i);
            // 提取右声道样本（16位有符号整数）
            short right = BitConverter.ToInt16(stereoData, i + 2);

            // 计算平均值（注意防止溢出）
            short monoValue = (short)((left + right) / 2);

            // 将结果写入单声道数据
            byte[] monoBytes = BitConverter.GetBytes(monoValue);
            monoData[i / 2] = monoBytes[0];     // 低字节
            monoData[i / 2 + 1] = monoBytes[1]; // 高字节
        }

        return monoData;
    }


    public void StopRecording()
    {
        hasDiscardedWavHeader = false;
        _cancellationTokenSource?.Cancel();
        OnRecordingStopped?.Invoke();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
    }
}
