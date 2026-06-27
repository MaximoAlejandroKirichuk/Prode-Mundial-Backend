# Tasks: Mercado Pago Registration Flow

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,100 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Domain/Persistence) → PR 2 (Checkout) → PR 3 (Webhook) |
| Delivery strategy | auto-forecast |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Depends |
|------|------|-----------|---------|
| 1 | Domain entities, DbContext, repos, interfaces, migration | PR 1 | — |
| 2 | Checkout use case, MP preference, controller + tests | PR 2 | PR 1 |
| 3 | Webhook use case, Resend email, webhook controller + tests | PR 3 | PR 2 |

TDD mode: `strict_tdd` — write failing test before each use-case implementation.

## Phase 1: Domain & Persistence Foundation (PR 1)

- [x] 1.1 Create `Api/Domain/Entities/Tournament.cs` — active flag, price amount, currency, closed-at
- [x] 1.2 Expand `Api/Domain/Entities/Registration.cs` — add `TournamentId` FK, payment timestamps, notification metadata, state transition methods
- [x] 1.3 Update `Api/Domain/Enums/RegistrationStatus.cs` — rename `PaidWithoutNotification` → `PaidPendingNotification`, add `Expired`, `ManualReview`
- [x] 1.4 Create `Api/Domain/Entities/RegistrationPayment.cs` — MP payment_id, amount, status snapshot
- [x] 1.5 Create `Api/Domain/Entities/WebhookDelivery.cs` — idempotency keyed by unique payment_id
- [x] 1.6 Create `Api/Domain/Entities/RegistrationAnomaly.cs` — orphan/mismatch/stale-link evidence
- [x] 1.7 Update `Api/Infrastructure/Persistence/AppDbContext.cs` — DbSets, configs, unique index on `WebhookDelivery.PaymentId`
- [x] 1.8 Create interfaces: `ITournamentRepository`, `IEmailService`, `IWebhookIdempotencyRepository`, `IRegistrationPaymentRepository`
- [x] 1.9 Extend `IRegistrationRepository` — add scoped duplicate query, external_reference lookup, status update
- [x] 1.10 Implement `TournamentRepository.cs` + extend `RegistrationRepository.cs` with new queries
- [x] 1.11 Add EF Core migration for new tables and indexes

## Phase 2: Checkout & Mercado Pago Integration (PR 2)

- [x] 2.1 Extend `IMercadoPagoService.cs` — add `GetPaymentAsync(paymentId)` method
- [x] 2.2 Implement `MercadoPagoService.cs` — SDK preference creation (`external_reference` = registration ID) + payment retrieval
- [x] 2.3 Modify `CreateRegistrationUseCase.cs` — validate tournament, scoped (email+tournament_id) duplicate rules (reuse <5min, block paid, retry after rejected/expired)
- [x] 2.4 Modify `CreateRegistrationRequest.cs` — add `[Required] TournamentId` field
- [x] 2.5 Modify `RegistrationsController.cs` — pass tournament_id, map errors to HTTP 422/409
- [x] 2.6 Write/update tests: checkout validation, duplicate rules, MP preference creation (test-first RED→GREEN)

## Phase 3: Webhook Processing & Notification (PR 3)

- [x] 3.1 Create `ProcessMercadoPagoWebhookUseCase.cs` — fetch MP payment, insert idempotency, validate integrity (amount/item), classify anomalies (orphan/mismatch/stale-link/late-approval), transition status
- [x] 3.2 Create `ResendEmailService.cs` — HTTP client with aggressive timeout, typed `ResendOptions`, email content with tournament/payment/access-link/support
- [x] 3.3 Create `MercadoPagoWebhookController.cs` — parse payment_id, delegate to use case, return 200
- [x] 3.4 Write tests: idempotent webhook, orphan handling, integrity mismatch, notification timeout, late-approval vs stale-link (test-first RED→GREEN)

## Phase 4: Wiring & Cleanup

- [x] 4.1 Register all new services in `Api/Program.cs`
- [x] 4.2 Remove `NotImplementedException` from `MercadoPagoService.cs`
- [x] 4.3 Verify full solution build passes
