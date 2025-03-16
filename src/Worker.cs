using System.Reflection.Metadata;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class Worker : IHostedService
{
    private readonly ILogger<Worker> _logger;
    private readonly AudioService _audioService;
    private readonly WebSocketService _webSocketService;
    private readonly LcdService _lcdService;

    private readonly SessionUpdateOptions _sessionUpdateOptions;

    private string nowUserMessage = "";

    private string nowAIMessage = "";

    public Worker(AudioService audioService, WebSocketService webSocketService, LcdService lcdService, ILogger<Worker> logger, IOptions<SessionUpdateOptions> sessionUpdateOptions)
    {
        _logger = logger;
        _sessionUpdateOptions = sessionUpdateOptions.Value;
        _lcdService = lcdService;
        
        _webSocketService = webSocketService;
        _webSocketService.OnMessageReceived += HandleWebSocketMessage;
        _webSocketService.OnConnected += HandleWebSocketConnected;

        _audioService = audioService;
        _audioService.OnAudioDataAvailable += HandleAudioDataAvailable;
        _audioService.OnPlaybackStopped += () =>
        {
            _lcdService.UpdateStatus(withImg: false);
            _audioService.StartRecordingAsync();
            _logger.LogInformation("OnPlaybackStopped and Recording");
        };
        
    }

    private async Task HandleAudioDataAvailable(byte[] obj)
    {
        // 处理接收到的音频数据  转为 base64
        var message = new BaseMessage
        {
            Type = "input_audio_buffer.append",
            Audio = Convert.ToBase64String(obj)
        }.ToJson();
        await _webSocketService.SendAsync(message);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _webSocketService.ConnectAsync();
        _lcdService.UpdateStatus();
        _audioService.StartRecordingAsync();
        _logger.LogInformation("Recording");
        //await TestSendAsync();
        //_audioService.RecordAudio("output.wav", 10);
        //_audioService.PlayAudio("output.wav");

        //await _webSocketService.SendAsync("Audio recording and playback completed.");
    }

    private async Task TestSendAsync(){
        await Task.Delay(2000);
        var pcmDate = await File.ReadAllBytesAsync("test.pcm");
        // 30 * 256 字节发送一次
        for (int i = 0; i < pcmDate.Length; i += 30 * 256)
        {
            var data = pcmDate.Skip(i).Take(30 * 256).ToArray();
            await HandleAudioDataAvailable(data);
            await Task.Delay(300);
        }
    }

    private async Task HandleWebSocketMessage(string message)
    {
        // 处理接收到的 WebSocket 消息 解析为 BaseMessage
        var baseMessage = JsonSerializer.Deserialize<BaseMessage>(message);
        if (baseMessage == null)
        {
            _logger.LogWarning("Received message is not a valid BaseMessage.");
            return;
        }
        switch (baseMessage.Type)
        {
            case "session.created":
                // 接入成功
                break;
            case "session.updated":
                // 更新配置成功
                _logger.LogInformation("Session updated.");
                _logger.LogInformation(message);
                break;
            case "input_audio_buffer.speech_started":
                // 用户开始说话
                _logger.LogInformation("User started speaking.");
                break;
            case "input_audio_buffer.speech_stopped":
                // 用户结束说话
                _logger.LogInformation("User stopped speaking.");
                break;
            case "input_audio_buffer.committed":
                // 用户音频提交
                _logger.LogInformation("User audio committed.");
                // 停止录音
                _audioService.StopRecording();
                break;
            case "conversation.item.input_audio_transcription.completed":
                // 用户音频转文本完成
                _logger.LogInformation($"User: {baseMessage.Transcript}");
                nowUserMessage = baseMessage.Transcript;
                // 更新LCD显示器上的用户文本
                await _lcdService.UpdateUserTextAsync(nowUserMessage);
                break;
            case "response.content_part.added":
                // AI 准备文本回复
                nowAIMessage = "";
                break;
            case "response.audio_transcript.delta":
                // AI 文本回复
                nowAIMessage += baseMessage.Delta;
                // 更新LCD显示器上的AI文本
                await _lcdService.UpdateAiTextAsync(nowAIMessage);
                //_logger.LogInformation(baseMessage.Delta);
                break;
            case "response.audio_transcript.done":
                // AI 文本回复完成
                _logger.LogInformation(nowAIMessage);
                break;
            case "response.audio.delta":
                // AI 音频回复
                // base64 解码
                var audioData = Convert.FromBase64String(baseMessage.Delta);
                await _audioService.SaveAudio(baseMessage.ResponseId, audioData);
                //await _audioService.PlayAudioAsync(audioData);
                break;
            case "response.audio.done":
                // 音频回复完成
                await _audioService.PlayAudio(baseMessage.ResponseId);
                break;
            case "conversation.item.created":
            case "response.output_item.added":
            case "response.output_item.done":
            case "response.content_part.done":
            case "response.created":
            case "response.done":
            case "rate_limits.updated":
                break;

            default:
                _logger.LogWarning($"Received message with unknown type: {baseMessage.Type}");
                _logger.LogWarning(message);
                break;
        }
    }

    private async Task HandleWebSocketConnected()
    {
        var upinfo = new
        {
            type = "session.update",
            session = _sessionUpdateOptions
        };
        var json = JsonSerializer.Serialize(upinfo);
        await _webSocketService.SendAsync(json);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopping...");
        _audioService.StopRecording();
        return _webSocketService.CloseAsync();
    }
}
