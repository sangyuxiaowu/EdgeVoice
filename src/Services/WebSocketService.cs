using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class WebSocketService : IDisposable
{
    private readonly ILogger<WebSocketService> _logger;
    private readonly RealtimeAPIOptions _options;
    private ClientWebSocket _client;
    

    public event Func<string, Task> OnMessageReceived = _ => Task.CompletedTask;
    public event Func<Task> OnConnected = () => Task.CompletedTask;
    public event Func<Task> OnDisconnected = () => Task.CompletedTask;

    public WebSocketService(IOptions<RealtimeAPIOptions> options, ILogger<WebSocketService> logger)
    {
        _options = options.Value;
        _client = new ClientWebSocket();
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var wssUrl = new Uri($"{(_options.IsSecure?"wss":"ws")}://{_options.Endpoint}/openai/realtime?api-version={_options.ApiVersion}&deployment={_options.Deployment}&api-key={_options.ApiKey}");

        _logger.LogInformation($"Connecting to {_options.Endpoint}");

        try
        {
            await _client.ConnectAsync(wssUrl, cancellationToken);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogError("WebSocket connection closed prematurely.");
            return;
        }

        _logger.LogInformation("WebSocket connected!");
        
        await OnConnected.Invoke();

        _ = ReceiveLoopAsync(cancellationToken);
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4];
        while (_client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                _logger.LogInformation("WebSocket closed!");

                await OnDisconnected.Invoke();
            }
            else
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await OnMessageReceived.Invoke(message);
                _logger.LogDebug($"Received: {message}");
            }
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
        _logger.LogInformation("WebSocket closed!");
        await OnDisconnected.Invoke();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
