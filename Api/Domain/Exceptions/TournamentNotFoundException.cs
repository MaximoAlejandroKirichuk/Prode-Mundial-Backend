namespace Api.Domain.Exceptions;

public sealed class TournamentNotFoundException(Guid tournamentId)
    : Exception($"Tournament '{tournamentId}' not found.")
{
    public Guid TournamentId { get; } = tournamentId;
}
