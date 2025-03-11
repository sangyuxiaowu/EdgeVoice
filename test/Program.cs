using Alsa.Net;
class Program
{
    static void Main(string[] args)
    {
        bool isRecording = true;
        string fileName = "output.wav";
        ushort channels = 2;

        if (args.Length > 1)
        {
            if (args[0].ToLower() == "play")
            {
                isRecording = false;
            }
            else if (args[0].ToLower() == "record")
            {
                isRecording = true;
            }

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("f="))
                {
                    fileName = args[i].Substring(2);
                }
                else if (args[i].StartsWith("c="))
                {
                    channels = ushort.Parse(args[i].Substring(2));
                }
            }
        }

        Console.WriteLine($"isRecording: {isRecording}, fileName: {fileName}, channels: {channels}");

        var settings = new SoundDeviceSettings
        {
            MixerDeviceName = "hw:0",       // 混音设备
            PlaybackDeviceName = "hw:0",     // 播放设备
            RecordingDeviceName = "hw:0",    // 录音设备
            RecordingSampleRate = 22050,     // 采样率
            RecordingBitsPerSample = 16,     // 采样位数
            RecordingChannels = channels     // 通道数
        };

        using var alsaDevice = AlsaDeviceBuilder.Create(settings);

        if (isRecording)
        {
            // 录制10秒音频
            Console.WriteLine("开始录音...");
            alsaDevice.Record(10, fileName);
        }
        else
        {
            // 播放录制的音频
            Console.WriteLine("播放音频...");
            alsaDevice.Play(fileName);
        }

    }

}