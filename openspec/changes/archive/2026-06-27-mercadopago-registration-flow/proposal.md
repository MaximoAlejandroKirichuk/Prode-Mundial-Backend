# Proposal: Mercado Pago Registration Flow

## Intent

Implement a production-ready Mercado Pago checkout and webhook flow for tournament registrations. The system must validate `tournament_id`, prevent duplicate paid access per (`email`, `tournament_id`), reuse recent pending checkouts for 5 minutes, and deliver access emails after approved payments while preserving anomalies and immutable retry history.

## Scope

### In Scope
- Extend checkout payload/validation with `name`, `email`, and active `tournament_id`.
- Create PostgreSQL-backed registration/payment lifecycle rules, including pending reuse, paid block, immutable retries, and `payment_id` idempotency.
- Integrate Mercado Pago preference creation, approved-payment webhook reconciliation, integrity checks, anomaly states, and automatic Resend notification.

### Out of Scope
- Manual resend tooling for operations.
- Broader backoffice workflows for anomaly resolution.

## Capabilities

### New Capabilities
- `mercadopago-webhook-processing`: Reconcile Mercado Pago payments, enforce idempotency, persist anomalies, and trigger notification outcomes.

### Modified Capabilities
- `registration-checkout`: Add `tournament_id` validation, scoped duplicate rules, pending reuse, immutable retries, and real Mercado Pago preference creation.

## Approach

Keep the existing Clean Architecture flow and replace placeholders in slices inside one change: first strengthen registration persistence/contracts, then generate a fresh Mercado Pago preference per new pending registration using `external_reference = registration.Id`, then add a webhook controller that validates approved status plus amount/item/tournament integrity, persists state, attempts Resend with aggressive timeout, and always returns HTTP 200 after persistence.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Api/Presentation/Contracts/Requests/CreateRegistrationRequest.cs` | Modified | Add `tournament_id`. |
| `Api/Application/UseCases/Registrations/CreateRegistrationUseCase.cs` | Modified | Enforce scoped duplicate/retry rules. |
| `Api/Infrastructure/Persistence/` | Modified | Add queries, updates, idempotency, anomaly persistence. |
| `Api/Infrastructure/Payments/` | Modified | Real Mercado Pago SDK preference/payment lookup. |
| `Api/Presentation/Controllers/` | Modified/New | Checkout adjustments and Mercado Pago webhook endpoint. |
| `Api/Infrastructure/Notifications/` | New | Resend integration for automatic email. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Fraud or metadata mismatch | Med | Require integrity checks and route to manual review state. |
| Duplicate or replayed webhooks | High | Persist unique `payment_id` and make processing idempotent. |
| Email timeout after payment | Med | Persist paid state first, mark `PaidPendingNotification`, retry later. |

## Rollback Plan

Disable Mercado Pago webhook route and SDK wiring, revert to current placeholder checkout behavior, and keep persisted payment/anomaly rows for audit without granting new automatic access.

## Dependencies

- Mercado Pago SDK credentials and webhook configuration.
- Resend API credentials.

## Success Criteria

- [ ] Checkout rejects invalid/inactive tournaments and reuses only recent pending registrations for the same (`email`, `tournament_id`).
- [ ] Approved webhooks persist idempotently, validate integrity, and classify orphan/mismatch/closed-tournament anomalies correctly.
- [ ] Successful approved payments grant access and mark `Notified`; notification failures become `PaidPendingNotification` without losing payment history.
