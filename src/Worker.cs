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

    private readonly SessionUpdateOptions _sessionUpdateOptions;

    private string nowUserMessage = "";

    private string nowAIMessage = "";

    public Worker(AudioService audioService, WebSocketService webSocketService, ILogger<Worker> logger, IOptions<SessionUpdateOptions> sessionUpdateOptions)
    {
        _logger = logger;
        _sessionUpdateOptions = sessionUpdateOptions.Value;
        
        _webSocketService = webSocketService;
        _webSocketService.OnMessageReceived += HandleWebSocketMessage;
        _webSocketService.OnConnected += HandleWebSocketConnected;

        _audioService = audioService;
        _audioService.OnAudioDataAvailable += HandleAudioDataAvailable;
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
        await _audioService.StartRecordingAsync();

        //_audioService.RecordAudio("output.wav", 10);
        //_audioService.PlayAudio("output.wav");

        //await _webSocketService.SendAsync("Audio recording and playback completed.");
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
            case "conversation.item.input_audio_transcription.completed":
                // 用户音频转文本完成
                _logger.LogInformation($"User: {baseMessage.Transcript}");
                nowUserMessage = baseMessage.Transcript;
                break;
            case "response.content_part.added":
                // AI 准备文本回复
                nowAIMessage = "";
                _logger.LogInformation("AI: ");
                break;
            case "response.audio_transcript.delta":
                // AI 文本回复
                nowAIMessage += baseMessage.Delta;
                _logger.LogInformation(baseMessage.Delta);
                break;
            default:
                _logger.LogWarning($"Received message with unknown type: {baseMessage.Type}");
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
        _audioService.StopRecording();
        return _webSocketService.CloseAsync();
    }
}
