namespace Api.Domain.Exceptions;

public sealed class TournamentNotActiveException(Guid tournamentId)
    : Exception($"Tournament '{tournamentId}' is not active.")
{
    public Guid TournamentId { get; } = tournamentId;
}
