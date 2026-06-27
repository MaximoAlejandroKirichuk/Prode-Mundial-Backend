namespace Api.Presentation.Contracts.Responses;

public sealed class ActiveTournamentResponse
{
    public Guid TournamentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal PriceAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
}
