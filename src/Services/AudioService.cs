using Alsa.Net;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

public class AudioService : IDisposable
{
    private readonly SoundDeviceSettings _settings;
    private CancellationTokenSource _cancellationTokenSource;
    private int _packetCount;
    private int _packetThreshold;
    private byte[] _buffer;
    private int _bufferIndex;

    public event Func<byte[], Task> OnAudioDataAvailable;
    public event Action OnRecordingStopped;

    /// <summary>
    /// 是否已经丢弃首包的WAV音频头
    /// </summary>
    private bool hasDiscardedWavHeader = false;

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
        _packetThreshold = 20; // 数据包阈值
        _packetCount = 0;
        _buffer = new byte[_packetThreshold * 1024]; // 初始缓冲区大小，假设每个包最大为1024字节
        _bufferIndex = 0;
    }

    public async Task StartRecordingAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        using var alsaDevice = AlsaDeviceBuilder.Create(_settings);
        await Task.Run(() => alsaDevice.Record(async (data) => 
        {
            if (OnAudioDataAvailable != null)
            {
                Console.WriteLine($"Received {data.Length} bytes of audio data.");
                if (!hasDiscardedWavHeader)
                {
                    hasDiscardedWavHeader = true;
                    return;
                }
                // 将数据改为单声道
                data = ConvertStereoToMono(data);

                // 如果缓冲区不够大，动态扩展缓冲区
                if (_bufferIndex + data.Length > _buffer.Length)
                {
                    Array.Resize(ref _buffer, _buffer.Length + _packetThreshold * 1024);
                }

                // 将数据写入缓冲区
                Array.Copy(data, 0, _buffer, _bufferIndex, data.Length);
                _bufferIndex += data.Length;
                _packetCount++;

                // 当数据包达到阈值时，触发事件
                if (_packetCount >= _packetThreshold)
                {
                    _packetCount = 0;
                    await Task.Run(() => OnAudioDataAvailable(_buffer[.._bufferIndex]));
                    _bufferIndex = 0;
                }
            }
        }, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
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
