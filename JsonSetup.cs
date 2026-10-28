using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyBot;

[JsonSerializable(typeof(Dictionary<string, UserData>))]
[JsonSerializable(typeof(Dictionary<string, TransactionData>))]
[JsonSerializable(typeof(Dictionary<string, KeyData>))]
[JsonSerializable(typeof(List<long>))]
[JsonSerializable(typeof(Database))]
public partial class BotJsonContext : JsonSerializerContext { }

public static class JsonSetup
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = BotJsonContext.Default,
    };

    public static JsonSerializerOptions GetOptions() => Options;
}
