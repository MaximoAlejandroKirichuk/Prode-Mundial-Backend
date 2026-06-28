# Design: Support Mercado Pago Body Webhook

## Technical Approach

Keep the existing webhook use case unchanged and fix the failure at the presentation boundary. `MercadoPagoWebhookController` will accept an optional JSON body plus optional query parameters, normalize them into the current `(topic, paymentIdStr)` contract, and continue returning HTTP 200 for all outcomes. This matches the proposal and avoids changes in payment reconciliation, idempotency, or registration state handling already implemented in `ProcessMercadoPagoWebhookUseCase`.

## Architecture Decisions

| Decision | Alternatives considered | Rationale |
|---|---|---|
| Normalize in the controller, not the use case | Introduce a new application-layer notification model | Smallest safe change. The use case already validates and processes `topic` + `paymentIdStr`; changing its signature would widen the blast radius. |
| Add a dedicated request DTO under `Api/Presentation/Contracts/Requests` | Read raw `Request.Body` manually; keep query-only binding | Follows the existing presentation contract pattern, keeps parsing explicit, and avoids mixing transport parsing into application code. |
| Use body-first, query-fallback precedence | Query-first; reject mixed payloads | Mercado Pago sends the authoritative webhook shape in JSON body. Query fallback preserves legacy compatibility without blocking current callers. |
| Store `data.id` as `JsonElement?` and normalize to string | `string` only; `long?` only; custom converter | Safest for Mercado Pago variability because `data.id` may arrive as string or number. No global serializer changes needed. |

## Data Flow

```text
Mercado Pago POST
   │
   ├─ query: topic,id (legacy/optional)
   └─ body: type,data.id (current/optional)
        │
        ▼
MercadoPagoWebhookController
   ├─ bind optional body with EmptyBodyBehavior.Allow
   ├─ normalize topic := body.type ?? query.topic
   └─ normalize paymentIdStr := body.data.id ?? query.id
        │
        ▼
ProcessMercadoPagoWebhookUseCase.ExecuteAsync(topic, paymentIdStr)
        │
        ▼
existing payment fetch + idempotency + registration updates
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Api/Presentation/Controllers/MercadoPagoWebhookController.cs` | Modify | Accept optional body/query inputs, normalize values, and keep HTTP 200 behavior. |
| `Api/Presentation/Contracts/Requests/MercadoPagoWebhookRequest.cs` | Create | Define the transport DTO for Mercado Pago webhook payloads, including nested `data.id`. |
| `tests/Api.Tests/Controllers/MercadoPagoWebhookControllerTests.cs` | Modify | Add normalization tests for body payload, query fallback, and numeric/string `data.id`. |
| `openspec/specs/mercadopago-webhook-processing/spec.md` | Modify | Clarify that the endpoint accepts Mercado Pago JSON body notifications and MAY fall back to query parameters. |

## Interfaces / Contracts

```csharp
public sealed class MercadoPagoWebhookRequest
{
    public string? Type { get; init; }
    public string? Action { get; init; }
    public MercadoPagoWebhookData? Data { get; init; }
}

public sealed class MercadoPagoWebhookData
{
    public JsonElement? Id { get; init; }
}
```

Controller signature target:

```csharp
public Task<IActionResult> HandleWebhook(
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MercadoPagoWebhookRequest? payload,
    [FromQuery] string? topic,
    [FromQuery] string? id,
    CancellationToken cancellationToken)
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit/controller | Body payload normalizes to existing use-case arguments | Direct controller tests with DTO instances and Moq verification. |
| Unit/controller | Query-only requests still work | Keep existing tests, updating for nullable parameters if needed. |
| Unit/controller | `data.id` numeric and string forms both map to the same `paymentIdStr` | Add explicit cases for `JsonElement` string/number payloads. |
| Manual smoke | Framework binding no longer returns 400 on Mercado Pago JSON | Verify with `Api.http` or Mercado Pago simulator because current tests invoke the controller directly and do not exercise ASP.NET Core model binding. |

## Migration / Rollout

No migration required. Rollout is a normal deploy because the use case contract and persistence model stay unchanged.

## Open Questions

- [ ] Should `action` remain unused but present in the DTO for parity/debugging, or should the DTO keep only `type` and `data.id`?
- [ ] Do we want a later follow-up integration test project for real model-binding coverage once the EF InMemory test mismatch is cleaned up?
