# Tasks: Support Mercado Pago Body Webhook

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 120–180 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | auto-forecast |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Payload DTO + controller normalization + tests + spec update | Single PR | All changes in one PR; under 180 lines |

## Phase 1: Foundation

- [x] 1.1 Create `Api/Presentation/Contracts/Requests/MercadoPagoWebhookRequest.cs` with `Type`, `Action`, `Data` (containing `JsonElement? Id`)

## Phase 2: Core Implementation

- [x] 2.1 Modify `MercadoPagoWebhookController.HandleWebhook` — add `[FromBody] MercadoPagoWebhookRequest? payload` and optional `[FromQuery]` params
- [x] 2.2 Add normalization: `topic = payload?.Type ?? topic`, `paymentIdStr = payload?.Data?.Id?.ToString() ?? id`
- [x] 2.3 Keep existing try/catch with HTTP 200 return unchanged

## Phase 3: Testing

- [x] 3.1 Add test: JSON body payload normalizes to use-case args (type=payment, data.id=123456)
- [x] 3.2 Add test: `data.id` as integer (non-string) maps to correct `paymentIdStr`
- [x] 3.3 Add test: query-only fallback preserves existing behavior
- [x] 3.4 Add test: body missing type falls back to query `topic`
- [x] 3.5 Add test: body missing `data.id` falls back to query `id`
- [x] 3.6 Update existing tests for new nullable parameter signature

## Phase 4: Documentation

- [x] 4.1 Update `openspec/specs/mercadopago-webhook-processing/spec.md` — clarify endpoint accepts JSON body and MAY fall back to query params
