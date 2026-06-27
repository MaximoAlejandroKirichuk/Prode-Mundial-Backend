using Api.Application.UseCases.Registrations;
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await createRegistrationUseCase.ExecuteAsync(
                request.Name, request.Email, cancellationToken);

            var response = new CreateRegistrationResponse
            {
                RegistrationId = result.RegistrationId,
                PaymentUrl = result.PaymentUrl,
                IsExisting = result.IsExisting
            };

            return CreatedAtAction(nameof(Create), new { id = result.RegistrationId }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
