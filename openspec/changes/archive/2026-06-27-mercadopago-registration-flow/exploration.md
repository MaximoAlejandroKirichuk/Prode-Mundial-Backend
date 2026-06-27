# Exploration: mercadopago-registration-flow

## Current State

The codebase has a Clean Architecture skeleton (Domain → Application → Infrastructure → Presentation) built on .NET 10 with PostgreSQL via EF Core. The checkout path for new registrations is wired end-to-end from controller to repository, but the Mercado Pago SDK integration, webhook handling, and email notification are still placeholders or missing.

The project uses:
- `Api/Program.cs` for DI wiring.
- `Api/Presentation/Controllers/RegistrationsController.cs` as the HTTP entry point.
- `Api/Application/UseCases/Registrations/CreateRegistrationUseCase.cs` for the registration checkout flow.
- `Api/Infrastructure/Persistence/` for EF Core + PostgreSQL.
- `Api/Infrastructure/Payments/MercadoPagoService.cs` as a placeholder service.

## Affected Areas

- `Api/Presentation/Controllers/RegistrationsController.cs` — existing checkout endpoint.
- `Api/Presentation/Contracts/Requests/CreateRegistrationRequest.cs` — name/email payload.
- `Api/Presentation/Contracts/Responses/CreateRegistrationResponse.cs` — payment URL response.
- `Api/Application/UseCases/Registrations/CreateRegistrationUseCase.cs` — 5-minute double-click guard and orchestration.
- `Api/Infrastructure/Persistence/Repositories/RegistrationRepository.cs` — minimal repository (only add + recent-pending query).
- `Api/Infrastructure/Persistence/AppDbContext.cs` — Registration entity mapping.
- `Api/Domain/Entities/Registration.cs` — entity with status lifecycle fields.
- `Api/Domain/Enums/RegistrationStatus.cs` — status values for reconciliation/notification.
- `Api/Infrastructure/Payments/MercadoPagoService.cs` — placeholder, not real SDK integration.
- `Api/Infrastructure/Payments/MercadoPagoOptions.cs` — access-token + sandbox options.
- `Api/Program.cs` — DI registration.
- `Api/Api.csproj` — package list (Npgsql.EFCore.PostgreSQL, no MercadoPago SDK, no Resend SDK).
- `docker-compose.yml` — PostgreSQL 17 + pgAdmin.

## Verification Checklist

### Implemented

1. **POST checkout endpoint**
   - `Api/Presentation/Controllers/RegistrationsController.cs` exposes `POST /api/registrations`.
   - Returns `201 Created` with `CreateRegistrationResponse`.

2. **Request payload with name and email**
   - `Api/Presentation/Contracts/Requests/CreateRegistrationRequest.cs` has `[Required]` `Name` and `[Required][EmailAddress]` `Email`.

3. **5-minute rule preventing multiple pending registrations**
   - `CreateRegistrationUseCase.cs` line 16 defines `RecentWindow = TimeSpan.FromMinutes(5)`.
   - Lines 31-40 call `repository.GetRecentPendingByEmailAsync(email, RecentWindow, ...)` and return the existing `PaymentUrl` when found.
   - Covered by test `ExecuteAsync_ShouldReturnExistingPaymentUrl_WhenRecentPendingExists`.

### Partially Implemented

4. **Persistence in PostgreSQL/SQL Server and status handling**
   - PostgreSQL is configured in `Api/Program.cs` (Npgsql) and `docker-compose.yml`.
   - `RegistrationStatus` enum has `Pending`, `Paid`, `Rejected`, `InReview`, `PaidWithoutNotification`, `Notified`.
   - **Gaps**: no EF Core migrations exist, repository only supports `AddAsync`, `SaveChangesAsync`, and `GetRecentPendingByEmailAsync`. No query-by-id or update methods for webhook processing.

5. **Mercado Pago preference creation with `external_reference`**
   - `CreateRegistrationUseCase.cs` line 46-47 calls `mercadoPagoService.CreatePreferenceAsync(name, email, registration.Id.ToString(), ...)`.
   - The registration ID is passed as `externalReference`.
   - **Gaps**: `MercadoPagoService.cs` is a placeholder that throws `NotSupportedException` when unconfigured and `NotImplementedException` when configured. No real SDK call is made.

6. **Response returns `init_point`**
   - `CreateRegistrationResponse.cs` returns `PaymentUrl`.
   - **Gaps**: the field is named `PaymentUrl`, not `init_point`. Whether the value will be Mercado Pago's `init_point` depends on the unimplemented SDK integration.

### Missing

7. **POST webhook endpoint**
   - No webhook controller or route exists. Grep for `webhook`, `Webhook` returned no files.

8. **Webhook idempotency against the database**
   - No payment/notification entity or logic exists to store processed webhook IDs and avoid duplicate processing.

9. **Mercado Pago payment status reconciliation**
   - No code queries the Mercado Pago SDK for a payment status.
   - No mapping of MP statuses (`approved`, `in_process`, `rejected`, `pending`) to `RegistrationStatus`.
   - No update logic for `Pending → Paid / Rejected / InReview`.

10. **HTTP call to Resend to send the Prode link when approved**
    - No Resend service, client, or abstraction exists.
    - No `PaidWithoutNotification` → `Notified` transition logic.
    - `Api/Api.csproj` does not reference a Resend SDK or generic mail package.

## Approaches

1. **Integrate Mercado Pago SDK + build webhook/email in one pass**
   - Pros: Delivers the full vertical flow in a single change.
   - Cons: Large surface area; webhook testing and idempotency are complex; exceeds comfortable review budget.
   - Effort: High

2. **Slice by capability: MP SDK checkout → webhook + reconciliation → Resend notification**
   - Pros: Each slice is independently reviewable and testable; matches the existing status enum lifecycle.
   - Cons: Requires orchestrating three dependent PRs.
   - Effort: Medium per slice, Medium overall

3. **Stub external dependencies behind interfaces, implement core state machine first**
   - Pros: Solid domain model and tests first; external SDKs can be swapped later.
   - Cons: Does not immediately return real `init_point` or send real emails.
   - Effort: Medium

## Recommendation

Use **Approach 2** (slice by capability). The current code already has clean abstractions (`IMercadoPagoService`, `IRegistrationRepository`) and a status enum that anticipates the full lifecycle, making it natural to extend in slices:
1. Replace the `MercadoPagoService` placeholder with the official Mercado Pago SDK and return real `init_point`.
2. Add a `POST /api/webhooks/mercadopago` controller, payment lookup, idempotency, and status reconciliation.
3. Add a `Resend` email service and the `PaidWithoutNotification → Notified` transition.

## Risks

- `MercadoPagoService.cs` currently throws at runtime; production will fail checkout until the SDK is integrated.
- No EF Core migrations exist; deploying persistence requires adding them.
- Webhook orphan edge case (MP webhook arrives before the pending registration is persisted) is not handled.
- InMemory EF Core version mismatch noted in `openspec/config.yaml` may block existing tests.

## Ready for Proposal

Yes. The codebase has a solid scaffold for the checkout path but lacks the external integrations. The next phase should produce a proposal scoped to the three slices above, including a rollback plan for the SDK/token configuration change.
