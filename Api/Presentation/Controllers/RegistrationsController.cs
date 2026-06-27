using Api.Application.UseCases.Registrations;
using Api.Domain.Exceptions;
using Api.Presentation.Contracts.Requests;
using Api.Presentation.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Api.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RegistrationsController(CreateRegistrationUseCase createRegistrationUseCase) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateRegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        try
        {
            var result = await createRegistrationUseCase.ExecuteAsync(
                request.Name, request.Email, request.TournamentId, cancellationToken);

            var response = new CreateRegistrationResponse
            {
                RegistrationId = result.RegistrationId,
                PaymentUrl = result.PaymentUrl,
                IsExisting = result.IsExisting
            };

            return CreatedAtAction(nameof(Create), new { id = result.RegistrationId }, response);
        }
        catch (TournamentNotFoundException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Tournament Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }
        catch (TournamentNotActiveException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Tournament Not Active",
                Detail = ex.Message,
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }
        catch (DuplicatePaidRegistrationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate Registration",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = ex.Message,
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Service Unavailable",
                Detail = ex.Message,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }
}
