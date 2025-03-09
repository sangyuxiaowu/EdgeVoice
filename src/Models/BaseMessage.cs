using System.Text.Json.Serialization;
public class BaseMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    [JsonPropertyName("audio")]
    public string? Audio { get; set; }

    
}