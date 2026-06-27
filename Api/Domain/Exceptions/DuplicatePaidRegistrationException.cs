namespace Api.Domain.Exceptions;

public sealed class DuplicatePaidRegistrationException(string email, Guid tournamentId)
    : Exception($"A paid registration already exists for '{email}' in tournament '{tournamentId}'.")
{
    public string Email { get; } = email;
    public Guid TournamentId { get; } = tournamentId;
}
