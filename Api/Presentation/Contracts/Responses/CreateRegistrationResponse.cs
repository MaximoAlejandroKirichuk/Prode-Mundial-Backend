namespace Api.Presentation.Contracts.Responses;

public sealed class CreateRegistrationResponse
{
    public Guid RegistrationId { get; init; }
    public string PaymentUrl { get; init; } = string.Empty;
    public bool IsExisting { get; init; }
}
