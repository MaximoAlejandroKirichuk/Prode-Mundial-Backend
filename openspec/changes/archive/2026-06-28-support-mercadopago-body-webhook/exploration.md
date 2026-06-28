# Exploration: support-mercadopago-body-webhook

## Current State

The webhook endpoint at `POST /api/mercadopago/webhook` is already implemented and delegates to `ProcessMercadoPagoWebhookUseCase.ExecuteAsync(topic, paymentIdStr, ...)`. The use case then fetches the authoritative payment from Mercado Pago, applies idempotency, validates integrity, and drives the registration status machine to `Paid` / `Notified`.

However, the controller currently binds only `[FromQuery] string topic` and `[FromQuery] string id`. Mercado Pago's webhook simulator and real payment webhooks post a JSON body such as:

```json
{
  "action": "payment.updated",
  "api_version": "v1",
  "data": { "id": "123456" },
  "date_created": "2021-11-01T02:02:02Z",
  "id": "123456",
  "live_mode": false,
  "type": "payment",
  "user_id": 724484980
}
```

Because the parameters are non-nullable reference types and the request arrives without query values, ASP.NET Core model validation returns HTTP 400. This matches the observed runtime behavior.

`MercadoPagoApi.CreatePreferenceAsync` sets `NotificationUrl = null`, relying on dashboard configuration. The URL is already configured correctly in the Mercado Pago dashboard, so no checkout change is needed.

## Affected Areas

- `Api/Presentation/Controllers/MercadoPagoWebhookController.cs` — must accept the JSON body and keep optional query-param compatibility.
- `tests/Api.Tests/Controllers/MercadoPagoWebhookControllerTests.cs` — needs tests for the body payload path; existing query-param tests can stay.
- `Api/Application/UseCases/Webhooks/ProcessMercadoPagoWebhookUseCase.cs` — no signature change is required if the controller normalizes inputs, but a small record could be introduced if preferred.
- `openspec/specs/mercadopago-webhook-processing/spec.md` — may need a delta clarifying the supported payload contract.

## Approaches

1. **Controller-only normalization (recommended)**
   - Add an optional `[FromBody] MercadoPagoWebhookPayload? payload` parameter.
   - Make query params `string? topic` and `string? id`.
   - Derive `paymentIdStr` from `payload?.Data?.Id` with fallback to `id`; derive `topic` from `payload?.Type` with fallback to `topic`.
   - Pass the same string values to the existing use case.
   - Define the DTO so `data.id` tolerates both string and number (e.g., `JsonElement` or a custom converter).
   - **Pros**: minimal surface; preserves existing use-case logic and tests; backward compatible with legacy IPN query parameters; easiest to verify.
   - **Cons**: payload parsing lives in the controller; introduces a small DTO.
   - **Effort**: Low

2. **Refactor use case to accept a webhook notification record**
   - Introduce a `MercadoPagoWebhookNotification` model and a thin parser.
   - The controller builds the model and passes it to the use case.
   - **Pros**: cleaner layering; payload parsing is unit-testable independent of the controller.
   - **Cons**: more files; requires updating existing controller and use-case tests.
   - **Effort**: Medium

3. **Add full webhook signature verification**
   - Implement Mercado Pago's `x-signature` header validation in addition to body parsing.
   - **Pros**: production-hardened security.
   - **Cons**: out of scope for the immediate 400 failure; requires a configured secret; significantly expands the change.
   - **Effort**: High

## Recommendation

Use **Approach 1** (controller-only normalization). It directly fixes the HTTP 400 from the simulator, accepts the real Mercado Pago body, and retains compatibility with query-param webhooks. The use case signature and business rules remain untouched.

## Risks

- `data.id` may arrive as a string or a number depending on the notification. A naïve `string` DTO property can fail deserialization for numeric values.
- Existing controller tests mock `ExecuteAsync("payment", "12345", ...)`. They will continue to pass if the method signature stays compatible, but new body-specific tests must be added.
- The test suite currently fails because of an EF Core InMemory version mismatch, so new tests may be difficult to run until that is resolved.
- Webhook signature verification is not addressed; the endpoint relies on URL secrecy. This is acceptable for the current scope but should be revisited later.

## Ready for Proposal

Yes. The change is narrow and well-scoped. The proposal should define the DTO shape, the body-over-query fallback precedence, the required controller tests, and whether the EF Core InMemory mismatch is fixed in this change or separately.
