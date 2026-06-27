using Api.Application.Abstractions.Persistence;
using Api.Presentation.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Api.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TournamentsController(ITournamentRepository tournamentRepository) : ControllerBase
{
    [HttpGet("active")]
    [ProducesResponseType(typeof(ActiveTournamentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var tournament = await tournamentRepository.GetActiveAsync(cancellationToken);

        if (tournament is null)
            return NotFound();

        var response = new ActiveTournamentResponse
        {
            TournamentId = tournament.Id,
            Name = tournament.Name,
            PriceAmount = tournament.PriceAmount,
            Currency = tournament.Currency
        };

        return Ok(response);
    }
}
