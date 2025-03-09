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

    public Worker(AudioService audioService, WebSocketService webSocketService, ILogger<Worker> logger, IOptions<SessionUpdateOptions> sessionUpdateOptions)
    {
        _sessionUpdateOptions = sessionUpdateOptions.Value;
        _audioService = audioService;
        _webSocketService = webSocketService;
        _logger = logger;
        _webSocketService.OnMessageReceived += HandleWebSocketMessage;
        _webSocketService.OnConnected += HandleWebSocketConnected;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _webSocketService.ConnectAsync();

        _audioService.RecordAudio("output.wav", 10);
        _audioService.PlayAudio("output.wav");

        await _webSocketService.SendAsync("Audio recording and playback completed.");
    }

    private async Task HandleWebSocketMessage(string message)
    {
        // 处理接收到的 WebSocket 消息
        Console.WriteLine($"Handling message: {message}");
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
        return _webSocketService.CloseAsync();
    }
}
