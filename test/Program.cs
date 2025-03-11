using Alsa.Net;

var settings = new SoundDeviceSettings
{
    MixerDeviceName = "hw:0",       // 混音设备
    PlaybackDeviceName = "hw:0",     // 播放设备
    RecordingDeviceName = "hw:0",    // 录音设备
    RecordingSampleRate = 16_000     // 16kHz采样率
};

using var alsaDevice = AlsaDeviceBuilder.Create(settings);

// 录制10秒音频
Console.WriteLine("开始录音...");
alsaDevice.Record(10, "output.wav");

// 播放录制的音频
Console.WriteLine("播放音频...");
alsaDevice.Play("output.wav");