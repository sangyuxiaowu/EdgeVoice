using System;
using System.IO;

public static class AudioUtils
{
    public static byte[] ConvertStereoToMono(byte[] stereoData)
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

    public static byte[] CreateWavHeader(int sampleRate = 22050, short bitsPerSample = 16, short channels = 1)
    {
        MemoryStream memoryStream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(memoryStream);

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(0); // 文件大小 - 8 字节
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16); // fmt chunk 大小
        writer.Write((short)1); // PCM 格式
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(0); // data chunk 大小

        writer.Seek(4, SeekOrigin.Begin);
        writer.Write((int)(memoryStream.Length - 8));

        writer.Seek(40, SeekOrigin.Begin);
        writer.Write(0); // data chunk 大小

        return memoryStream.ToArray();
    }
}
