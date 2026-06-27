using System.ComponentModel.DataAnnotations;

namespace Api.Presentation.Contracts.Requests;

public sealed class CreateRegistrationRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public Guid TournamentId { get; init; }
}
