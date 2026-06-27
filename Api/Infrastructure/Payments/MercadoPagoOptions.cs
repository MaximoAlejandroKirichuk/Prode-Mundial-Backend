namespace Api.Infrastructure.Payments;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public string AccessToken { get; set; } = string.Empty;
    public bool UseSandbox { get; set; } = true;
}
