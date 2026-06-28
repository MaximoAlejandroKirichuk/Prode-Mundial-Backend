# Proposal: Support Mercado Pago Body Webhook

## Intent

Accept Mercado Pago webhook JSON bodies at `POST /api/mercadopago/webhook` so real and simulated payment notifications stop failing with HTTP 400 and approved payments can reach the existing processing flow.

## Scope

### In Scope
- Accept optional JSON body fields such as `type`, `action`, and `data.id`.
- Normalize body and query inputs into the existing `(topic, paymentIdStr)` use-case contract.
- Add controller tests for body payloads while preserving query-param compatibility.

### Out of Scope
- Mercado Pago signature verification.
- Checkout preference or dashboard webhook URL changes.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `mercadopago-webhook-processing`: clarify that the webhook endpoint MUST accept Mercado Pago JSON body notifications and MAY fall back to legacy query parameters.

## Approach

Use controller-level normalization. Make query params optional, add an optional body DTO, extract `topic` from `payload.type` with query fallback, and extract `paymentIdStr` from `payload.data.id` with query fallback. Keep the existing use case signature unchanged. The DTO must tolerate `data.id` arriving as either string or number.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Api/Presentation/Controllers/MercadoPagoWebhookController.cs` | Modified | Accept and normalize body/query webhook inputs |
| `tests/Api.Tests/Controllers/MercadoPagoWebhookControllerTests.cs` | Modified | Cover body payload and fallback behavior |
| `openspec/specs/mercadopago-webhook-processing/spec.md` | Modified | Document supported webhook request contract |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `data.id` type mismatch breaks deserialization | Med | Use tolerant parsing for string/number values |
| Regression for existing query-param callers | Low | Preserve fallback precedence and keep existing tests |
| Test execution remains blocked by EF InMemory mismatch | Med | Keep proposal scoped; fix test dependency separately if needed |

## Rollback Plan

Revert controller binding changes and DTO additions, restore query-only webhook handling, and remove body-specific tests/spec delta.

## Dependencies

- Existing `ProcessMercadoPagoWebhookUseCase.ExecuteAsync(topic, paymentIdStr, ...)` contract remains the downstream dependency.

## Success Criteria

- [ ] Mercado Pago simulator body payload no longer returns HTTP 400 for valid payment notifications.
- [ ] Body and query webhook formats both reach the existing webhook use case with normalized values.
