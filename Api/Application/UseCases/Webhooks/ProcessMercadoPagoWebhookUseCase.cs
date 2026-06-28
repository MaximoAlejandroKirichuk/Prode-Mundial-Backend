using Api.Application.Abstractions.Notifications;
using Api.Application.Abstractions.Payments;
using Api.Application.Abstractions.Persistence;
using Api.Domain.Entities;
using Api.Domain.Enums;

namespace Api.Application.UseCases.Webhooks;

public enum WebhookOutcome
{
    AlreadyProcessed,
    Processed,
    Orphan,
    Mismatch,
    LateApproval,
    StalePayment,
    NotificationFailed,
    Rejected,
    TransientFailure
}

public sealed record ProcessWebhookResult(WebhookOutcome Outcome, string? Description = null);

public class ProcessMercadoPagoWebhookUseCase(
    IMercadoPagoService mercadoPagoService,
    IRegistrationRepository registrationRepository,
    IWebhookIdempotencyRepository idempotencyRepository,
    IRegistrationPaymentRepository paymentRepository,
    IRegistrationAnomalyRepository anomalyRepository,
    IEmailService emailService)
{
    private static readonly TimeSpan LateApprovalWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan StalePaymentThreshold = TimeSpan.FromDays(30);

    public virtual async Task<ProcessWebhookResult> ExecuteAsync(
        string topic,
        string paymentIdStr,
        CancellationToken cancellationToken = default)
    {
        // ---- Step 1: Input validation ----
        if (string.IsNullOrWhiteSpace(paymentIdStr))
            throw new ArgumentException("PaymentId is required.", nameof(paymentIdStr));

        if (!long.TryParse(paymentIdStr, out var paymentId))
            throw new ArgumentException("PaymentId must be a numeric value.", nameof(paymentIdStr));

        var now = DateTimeOffset.UtcNow;

        // ---- Step 2: Fetch authoritative payment from Mercado Pago ----
        MercadoPagoPayment mpPayment;
        try
        {
            mpPayment = await mercadoPagoService.GetPaymentAsync(paymentId, cancellationToken);
        }
        catch
        {
            // Transient failure — do NOT persist anything so Mercado Pago retries get a clean path.
            return new ProcessWebhookResult(WebhookOutcome.TransientFailure,
                "Failed to fetch payment from Mercado Pago; will retry on next notification.");
        }

        // ---- Step 3: Idempotency check (paymentId + status) ----
        var status = mpPayment.Status;
        if (await idempotencyRepository.IsDuplicateAsync(paymentIdStr, status, cancellationToken))
        {
            return new ProcessWebhookResult(WebhookOutcome.AlreadyProcessed,
                $"Payment {paymentIdStr} with status '{status}' was already processed.");
        }

        // ---- Step 4: Record idempotency delivery (not yet processed) ----
        var delivery = new WebhookDelivery(paymentIdStr, topic, status);
        await idempotencyRepository.AddAsync(delivery, cancellationToken);

        // ---- Step 5: Look up registration by external_reference ----
        Registration? registration = null;
        if (mpPayment.ExternalReference is not null)
        {
            registration = await registrationRepository.GetByExternalReferenceAsync(
                mpPayment.ExternalReference, cancellationToken);
        }

        // ---- Step 6: Orphan — no registration found ----
        if (registration is null)
        {
            var orphanAnomaly = new RegistrationAnomaly(
                null, "OrphanWebhook",
                $"Payment {paymentIdStr} has external_reference '{mpPayment.ExternalReference}' with no matching registration. Topic: {topic}.");

            await anomalyRepository.AddAsync(orphanAnomaly, cancellationToken);

            delivery.MarkProcessed();
            await idempotencyRepository.SaveChangesAsync(cancellationToken);
            await anomalyRepository.SaveChangesAsync(cancellationToken);

            return new ProcessWebhookResult(WebhookOutcome.Orphan,
                "No registration found for external reference.");
        }

        // ---- Step 7: Persist payment snapshot ----
        var paymentSnapshot = new RegistrationPayment(
            registration.Id,
            paymentIdStr,
            mpPayment.Amount,
            mpPayment.Currency,
            mpPayment.Status);

        await paymentRepository.AddAsync(paymentSnapshot, cancellationToken);

        // ---- Step 8: Handle non-approved payments ----
        if (!string.Equals(mpPayment.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            // Guard: if registration is already in a terminal paid state from a prior approved
            // webhook, don't downgrade it. Pending/rejected statuses can safely transition.
            if (registration.Status is RegistrationStatus.Paid
                or RegistrationStatus.Notified
                or RegistrationStatus.PaidPendingNotification)
            {
                delivery.MarkProcessed();
                await idempotencyRepository.SaveChangesAsync(cancellationToken);
                await paymentRepository.SaveChangesAsync(cancellationToken);

                return new ProcessWebhookResult(WebhookOutcome.AlreadyProcessed,
                    "Registration already approved; ignoring late non-approved notification.");
            }

            registration.MarkAsRejected(now, $"Mercado Pago payment status: {mpPayment.Status}");
            await registrationRepository.UpdateStatusAsync(registration, cancellationToken);

            delivery.MarkProcessed();
            await idempotencyRepository.SaveChangesAsync(cancellationToken);
            await paymentRepository.SaveChangesAsync(cancellationToken);
            await registrationRepository.SaveChangesAsync(cancellationToken);

            return new ProcessWebhookResult(WebhookOutcome.Rejected,
                $"Payment status is '{mpPayment.Status}', not 'approved'.");
        }

        // ---- Step 9: Integrity validation ----
        var tournament = registration.Tournament;

        if (mpPayment.Amount != tournament.PriceAmount)
        {
            var mismatchAnomaly = new RegistrationAnomaly(
                registration.Id, "IntegrityMismatch",
                $"Payment {paymentIdStr}: amount {mpPayment.Amount} {mpPayment.Currency} vs expected {tournament.PriceAmount} {tournament.Currency}.");

            await anomalyRepository.AddAsync(mismatchAnomaly, cancellationToken);

            // Guard against downgrading a terminal paid state from a prior webhook.
            if (registration.Status is not RegistrationStatus.Paid
                and not RegistrationStatus.Notified
                and not RegistrationStatus.PaidPendingNotification)
            {
                registration.MarkAsManualReview(
                    $"Amount mismatch: expected {tournament.PriceAmount} {tournament.Currency}, got {mpPayment.Amount} {mpPayment.Currency}.");
                await registrationRepository.UpdateStatusAsync(registration, cancellationToken);
            }

            delivery.MarkProcessed();
            await idempotencyRepository.SaveChangesAsync(cancellationToken);
            await paymentRepository.SaveChangesAsync(cancellationToken);
            await anomalyRepository.SaveChangesAsync(cancellationToken);
            await registrationRepository.SaveChangesAsync(cancellationToken);

            return new ProcessWebhookResult(WebhookOutcome.Mismatch,
                "Payment amount does not match tournament price.");
        }

        if (!string.Equals(mpPayment.Currency, tournament.Currency, StringComparison.OrdinalIgnoreCase))
        {
            var mismatchAnomaly = new RegistrationAnomaly(
                registration.Id, "IntegrityMismatch",
                $"Payment {paymentIdStr}: currency {mpPayment.Currency} vs expected {tournament.Currency}.");

            await anomalyRepository.AddAsync(mismatchAnomaly, cancellationToken);

            // Guard against downgrading a terminal paid state from a prior webhook.
            if (registration.Status is not RegistrationStatus.Paid
                and not RegistrationStatus.Notified
                and not RegistrationStatus.PaidPendingNotification)
            {
                registration.MarkAsManualReview(
                    $"Currency mismatch: expected {tournament.Currency}, got {mpPayment.Currency}.");
                await registrationRepository.UpdateStatusAsync(registration, cancellationToken);
            }

            delivery.MarkProcessed();
            await idempotencyRepository.SaveChangesAsync(cancellationToken);
            await paymentRepository.SaveChangesAsync(cancellationToken);
            await anomalyRepository.SaveChangesAsync(cancellationToken);
            await registrationRepository.SaveChangesAsync(cancellationToken);

            return new ProcessWebhookResult(WebhookOutcome.Mismatch,
                "Payment currency does not match tournament currency.");
        }

        // ---- Step 10: Defense-in-depth — registration created after tournament closed ----
        if (tournament.ClosedAt is not null && registration.CreatedAt > tournament.ClosedAt.Value)
        {
            // Guard against downgrading a terminal paid state.
            if (registration.Status is RegistrationStatus.Paid
                or RegistrationStatus.Notified
                or RegistrationStatus.PaidPendingNotification)
            {
                delivery.MarkProcessed();
                await idempotencyRepository.SaveChangesAsync(cancellationToken);
                await paymentRepository.SaveChangesAsync(cancellationToken);

                return new ProcessWebhookResult(WebhookOutcome.AlreadyProcessed,
                    "Registration already approved; stale-creation check skipped.");
            }

            registration.MarkAsRejected(now,
                $"Registration created {registration.CreatedAt:O} after tournament closed {tournament.ClosedAt.Value:O}.");

            var closedAnomaly = new RegistrationAnomaly(
                registration.Id, "StaleClosedTournamentPayment",
                $"Payment {paymentIdStr}: registration created after tournament was already closed.");

            await anomalyRepository.AddAsync(closedAnomaly, cancellationToken);
            await registrationRepository.UpdateStatusAsync(registration, cancellationToken);

            delivery.MarkProcessed();
            await idempotencyRepository.SaveChangesAsync(cancellationToken);
            await paymentRepository.SaveChangesAsync(cancellationToken);
            await anomalyRepository.SaveChangesAsync(cancellationToken);
            await registrationRepository.SaveChangesAsync(cancellationToken);

            return new ProcessWebhookResult(WebhookOutcome.StalePayment,
                "Registration was created after tournament closure.");
        }

        // ---- Step 11: Tournament closure classification ----
        if (tournament.ClosedAt is not null)
        {
            var timeSinceClosed = now - tournament.ClosedAt.Value;

            // Stale payment: tournament closed > 30 days ago
            if (timeSinceClosed > StalePaymentThreshold)
            {
                // Guard against downgrading a terminal paid state.
                if (registration.Status is RegistrationStatus.Paid
                    or RegistrationStatus.Notified
                    or RegistrationStatus.PaidPendingNotification)
                {
                    delivery.MarkProcessed();
                    await idempotencyRepository.SaveChangesAsync(cancellationToken);
                    await paymentRepository.SaveChangesAsync(cancellationToken);

                    return new ProcessWebhookResult(WebhookOutcome.AlreadyProcessed,
                        "Registration already approved; stale-payment check skipped.");
                }

                registration.MarkAsRejected(now, $"Stale payment: tournament closed {tournament.ClosedAt.Value:O}.");

                var staleAnomaly = new RegistrationAnomaly(
                    registration.Id, "StalePayment",
                    $"Payment {paymentIdStr} targets tournament closed {timeSinceClosed.TotalDays:F0} days ago.");

                await anomalyRepository.AddAsync(staleAnomaly, cancellationToken);
                await registrationRepository.UpdateStatusAsync(registration, cancellationToken);

                delivery.MarkProcessed();
                await idempotencyRepository.SaveChangesAsync(cancellationToken);
                await paymentRepository.SaveChangesAsync(cancellationToken);
                await anomalyRepository.SaveChangesAsync(cancellationToken);
                await registrationRepository.SaveChangesAsync(cancellationToken);

                return new ProcessWebhookResult(WebhookOutcome.StalePayment,
                    "Payment targets a long-closed tournament.");
            }

            // Late approval: tournament closed < 24 hours ago
            // The registration was created while the tournament was active.
            if (timeSinceClosed < LateApprovalWindow && registration.CreatedAt <= tournament.ClosedAt.Value)
            {
                // Guard against downgrading a terminal paid state.
                if (registration.Status is RegistrationStatus.Paid
                    or RegistrationStatus.Notified
                    or RegistrationStatus.PaidPendingNotification)
                {
                    delivery.MarkProcessed();
                    await idempotencyRepository.SaveChangesAsync(cancellationToken);
                    await paymentRepository.SaveChangesAsync(cancellationToken);

                    return new ProcessWebhookResult(WebhookOutcome.AlreadyProcessed,
                        "Registration already approved; late-approval check skipped.");
                }

                registration.MarkAsManualReview(
                    $"Late approval: payment approved after tournament closed at {tournament.ClosedAt.Value:O}.");

                var lateAnomaly = new RegistrationAnomaly(
                    registration.Id, "LateApproval",
                    $"Payment {paymentIdStr} approved {timeSinceClosed.TotalHours:F1}h after tournament closed.");

                await anomalyRepository.AddAsync(lateAnomaly, cancellationToken);
                await registrationRepository.UpdateStatusAsync(registration, cancellationToken);

                delivery.MarkProcessed();
                await idempotencyRepository.SaveChangesAsync(cancellationToken);
                await paymentRepository.SaveChangesAsync(cancellationToken);
                await anomalyRepository.SaveChangesAsync(cancellationToken);
                await registrationRepository.SaveChangesAsync(cancellationToken);

                return new ProcessWebhookResult(WebhookOutcome.LateApproval,
                    "Approved payment for recently-closed tournament; flagged for review.");
            }
        }

        // ---- Step 12: Approved happy path ----
        var paidAt = mpPayment.CreatedAt != default ? mpPayment.CreatedAt : now;
        registration.MarkAsPaid(paidAt);
        await registrationRepository.UpdateStatusAsync(registration, cancellationToken);

        delivery.MarkProcessed();
        await idempotencyRepository.SaveChangesAsync(cancellationToken);
        await paymentRepository.SaveChangesAsync(cancellationToken);
        await registrationRepository.SaveChangesAsync(cancellationToken);

        // ---- Step 13: Attempt notification ----
        try
        {
            var emailResult = await emailService.SendAccessEmailAsync(
                registration.Email,
                registration.Name,
                tournament.Name,
                paidAt,
                cancellationToken);

            if (emailResult.Success)
            {
                registration.MarkAsNotified(now);
                await registrationRepository.UpdateStatusAsync(registration, cancellationToken);
                await registrationRepository.SaveChangesAsync(cancellationToken);

                return new ProcessWebhookResult(WebhookOutcome.Processed,
                    "Payment approved and access email sent.");
            }
            else
            {
                registration.MarkAsPaymentNotificationFailed();
                await registrationRepository.UpdateStatusAsync(registration, cancellationToken);
                await registrationRepository.SaveChangesAsync(cancellationToken);

                return new ProcessWebhookResult(WebhookOutcome.NotificationFailed,
                    $"Email delivery failed: {emailResult.ErrorMessage}");
            }
        }
        catch
        {
            registration.MarkAsPaymentNotificationFailed();
            await registrationRepository.UpdateStatusAsync(registration, cancellationToken);
            await registrationRepository.SaveChangesAsync(cancellationToken);

            return new ProcessWebhookResult(WebhookOutcome.NotificationFailed,
                "Email delivery threw an exception.");
        }
    }
}
