# 发布

```bash
dotnet publish ./src/CsharpWssRealtimeAPI.csproj -r linux-arm -p:PublishSingleFile=true -f net9.0 --self-contained=false -o ./publish/linux-arm
```

- 树莓派需要修改 `linux-arm` 为 `linux-arm64`
- 如果需要框架依赖，删除 `--self-contained=false` 参数即可
- 使用的库 Alsa 不支持 Windows，所以 Windows 下无法运行


# 代办

播放音频时候多个实例依次播放，每次初始化播放设备都会刚开始播放时出现刺耳的高音异响。改为每个音频持续写入音频文件，然后播放文件试试。
同时也可以保存音频文件。