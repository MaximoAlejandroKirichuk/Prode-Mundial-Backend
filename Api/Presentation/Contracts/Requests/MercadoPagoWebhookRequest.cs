using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Presentation.Contracts.Requests;

public sealed class MercadoPagoWebhookRequest
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("data")]
    public MercadoPagoWebhookData? Data { get; init; }
}

public sealed class MercadoPagoWebhookData
{
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }
}
