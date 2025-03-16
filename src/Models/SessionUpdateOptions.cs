using System.Text.Json.Serialization;

public class SessionUpdateOptions
{
    [JsonPropertyName("instructions")]
    public string Instructions { get; set; }
    
    [JsonPropertyName("voice")]
    public string Voice { get; set; }

    [JsonPropertyName("input_audio_transcription")]
    public InputAudioTranscription InputAudioTranscription { get; set; }
    
    [JsonPropertyName("turn_detection")]
    public TurnDetection TurnDetection { get; set; }

    [JsonPropertyName("max_response_output_tokens")]
    public int MaxResponseOutputTokens { get; set; }
    
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    public SessionUpdateOptions()
    {
        Instructions = "";
        Voice = "alloy";
        TurnDetection = new TurnDetection();
        Temperature = 0.5;
        MaxResponseOutputTokens = 1000;
        InputAudioTranscription = new InputAudioTranscription();
    }
}

public class InputAudioTranscription{
    
    [JsonPropertyName("model")]
    public string Model { get; set; }

    public InputAudioTranscription()
    {
        Model = "whisper-1";
    }
}


public class TurnDetection{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }

    [JsonPropertyName("prefix_padding_ms")]
    public int PrefixPadding { get; set; }

    [JsonPropertyName("silence_duration_ms")]
    public int SilenceDuration { get; set; }

    [JsonPropertyName("create_response")]
    public bool CreateResponse { get; set; }

    public TurnDetection()
    {
        Type = "server_vad";
        Threshold = 0.5;
        PrefixPadding = 300;
        SilenceDuration = 200;
        CreateResponse = true;
    }

    public TurnDetection(string type, double threshold, int prefixPadding, int silenceDuration, bool createResponse)
    {
        Type = type;
        Threshold = threshold;
        PrefixPadding = prefixPadding;
        SilenceDuration = silenceDuration;
        CreateResponse = createResponse;
    }

    public TurnDetection(string type)
    {
        Type = type;
    }
}
