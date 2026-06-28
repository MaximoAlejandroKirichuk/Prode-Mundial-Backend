using Api.Application.UseCases.Webhooks;
using Api.Presentation.Contracts.Requests;
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
    /// Accepts JSON body (real Mercado Pago notification format) with query-param
    /// fallback for legacy callers. Always returns HTTP 200 on success to prevent MP
    /// from retrying — even on errors. Returns 400 when neither body nor query
    /// supplies the required values.
    /// </summary>
    [HttpPost("webhook")]
    public virtual async Task<IActionResult> HandleWebhook(
        [FromBody] MercadoPagoWebhookRequest? payload,
        [FromQuery] string? topic,
        [FromQuery] string? id,
        CancellationToken cancellationToken)
    {
        // Normalize: body-first, query-fallback.
        var resolvedTopic = payload?.Type ?? topic;
        var resolvedPaymentIdStr = payload?.Data?.Id?.ToString() ?? id;

        if (string.IsNullOrWhiteSpace(resolvedTopic)
            || string.IsNullOrWhiteSpace(resolvedPaymentIdStr))
        {
            return BadRequest(new
            {
                error = "validation_error",
                message = "Either a valid JSON body (type + data.id) or query parameters (topic + id) must be provided."
            });
        }

        try
        {
            await _useCase.ExecuteAsync(resolvedTopic, resolvedPaymentIdStr, cancellationToken);
        }
        catch
        {
            // Silently catch: we always return 200 to Mercado Pago.
            // Errors are logged/persisted inside the use case.
        }

        return Ok();
    }
}
