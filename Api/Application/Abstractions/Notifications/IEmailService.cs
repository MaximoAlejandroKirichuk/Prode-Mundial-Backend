namespace Api.Application.Abstractions.Notifications;

public interface IEmailService
{
    Task<EmailSendResult> SendAccessEmailAsync(
        string toEmail,
        string toName,
        string tournamentName,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken = default);
}

public sealed record EmailSendResult(bool Success, string? ErrorMessage = null);
