# EdgeVoice

## 项目说明

EdgeVoice 是一个使用 Azure OpenAI Realtime API 实现的一个 AI 聊天机器人。


## 发布

```bash
dotnet publish ./src/EdgeVoice.csproj -r linux-arm -p:PublishSingleFile=true -f net9.0 --self-contained=false -o ./publish/linux-arm
```

- 树莓派需要修改 `linux-arm` 为 `linux-arm64`
- 如果需要框架依赖，删除 `--self-contained=false` 参数即可
- 使用的库 Alsa 不支持 Windows，所以 Windows 下无法运行