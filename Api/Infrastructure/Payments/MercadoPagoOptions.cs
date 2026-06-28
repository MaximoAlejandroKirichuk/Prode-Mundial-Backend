namespace Api.Infrastructure.Payments;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public string AccessToken { get; set; } = string.Empty;
    public bool UseSandbox { get; set; } = true;
    public BackUrlsConfig? BackUrls { get; set; }
    public string? AutoReturn { get; set; }
}

public sealed class BackUrlsConfig
{
    public string? Success { get; set; }
    public string? Failure { get; set; }
    public string? Pending { get; set; }
}
