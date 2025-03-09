using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class WebSocketService
{
    private readonly ILogger<WebSocketService> _logger;
    private readonly RealtimeAPIOptions _options;
    private ClientWebSocket _client;
    

    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public WebSocketService(IOptions<RealtimeAPIOptions> options, ILogger<WebSocketService> logger)
    {
        _options = options.Value;
        _client = new ClientWebSocket();
        _logger = logger;
    }

    public async Task ConnectAsync()
    {
        var wssUrl = new Uri($"wss://{_options.Endpoint}/openai/realtime?api-version={_options.ApiVersion}&deployment={_options.Deployment}&api-key={_options.ApiKey}");

        _logger.LogInformation($"Connecting to {_options.Endpoint}");

        try
        {
            await _client.ConnectAsync(wssUrl, CancellationToken.None);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogError("WebSocket connection closed prematurely.");
            return;
        }

        _logger.LogInformation("WebSocket connected!");
        
        OnConnected?.Invoke();

        _ = ReceiveLoopAsync();
    }

    public async Task SendAsync(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[1024 * 4];
        while (_client.State == WebSocketState.Open)
        {
            var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                Console.WriteLine("WebSocket closed!");
            }
            else
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                OnMessageReceived?.Invoke(message);
                Console.WriteLine($"Received: {message}");
            }
        }
    }

    public async Task CloseAsync()
    {
        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        Console.WriteLine("WebSocket closed!");
    }
}
