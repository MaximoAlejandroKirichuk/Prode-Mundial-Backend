namespace Api.Application.Options;

public sealed class AccessLinkOptions
{
    public const string SectionName = "AccessLink";

    public string Template { get; set; } = "https://example.com/access/{registrationId}";
}
