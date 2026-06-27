using Api.Application.UseCases.Webhooks;
using Microsoft.AspNetCore.Mvc;

namespace Api.Presentation.Controllers;

[ApiController]
[Route("api/mercadopago")]
public class MercadoPagoWebhookController : ControllerBase
{
    private readonly ProcessMercadoPagoWebhookUseCase _useCase;

    public MercadoPagoWebhookController(ProcessMercadoPagoWebhookUseCase useCase)
    {
        _useCase = useCase;
    }

    /// <summary>
    /// Receives Mercado Pago payment notifications and processes them idempotently.
    /// Always returns HTTP 200 to prevent MP from retrying — even on errors.
    /// </summary>
    [HttpPost("webhook")]
    public virtual async Task<IActionResult> HandleWebhook(
        [FromQuery] string topic,
        [FromQuery] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            await _useCase.ExecuteAsync(topic, id, cancellationToken);
        }
        catch
        {
            // Silently catch: we always return 200 to Mercado Pago.
            // Errors are logged/persisted inside the use case.
        }

        return Ok();
    }
}
