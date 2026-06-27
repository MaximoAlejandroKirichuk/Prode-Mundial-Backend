namespace Api.Infrastructure.Notifications;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;
    public string FromEmail { get; set; } = string.Empty;
}
