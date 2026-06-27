# mercadopago-webhook-processing Specification

## Purpose

Process Mercado Pago payment notifications idempotently, reconcile payment status, validate integrity, classify anomalies, and trigger notification delivery for approved tournament registrations.

## Requirements

### Requirement: Idempotent webhook processing

The system MUST persist the Mercado Pago `payment_id` as a unique key and MUST NOT process the same payment twice. Repeated notifications for an already-processed payment_id MUST return HTTP 200 without side effects.

#### Scenario: Duplicate webhook

- GIVEN payment_id 12345 was already processed
- WHEN the webhook for payment_id 12345 is received again
- THEN HTTP 200 is returned and no state change occurs

#### Scenario: First-time webhook

- GIVEN payment_id 67890 has not been processed
- WHEN the webhook is received
- THEN the payment_id is persisted and processing proceeds

### Requirement: Authoritative payment status retrieval and integrity validation

The system MUST fetch the authoritative payment status from the Mercado Pago API using the `payment_id`. It MUST validate that the amount, item, and tournament context match the stored registration. On mismatch, the registration MUST be flagged `InReview`.

#### Scenario: Integrity match

- GIVEN a webhook with payment_id linked to registration R
- WHEN the MP API returns status=approved with matching amount and item
- THEN processing continues to the approved path

#### Scenario: Amount mismatch

- GIVEN a webhook where the MP payment amount differs from the registration amount
- WHEN integrity is validated
- THEN registration status becomes InReview and HTTP 200 is returned

### Requirement: Approved payment happy path (Paid → Notified)

When an approved payment passes integrity checks, the system MUST transition the registration to `Paid`, attempt Resend email delivery, and transition to `Notified` on success.

#### Scenario: Successful notification

- GIVEN an approved payment with valid integrity
- WHEN the webhook is processed
- THEN registration status becomes Paid
- AND Resend sends the access link email
- AND registration status becomes Notified

### Requirement: Notification failure path (PaidPendingNotification)

When Resend delivery fails (timeout or error), the system MUST persist `PaidPendingNotification` status and MUST return HTTP 200 to Mercado Pago to prevent retry storms. The payment history MUST be preserved.

#### Scenario: Email delivery timeout

- GIVEN an approved payment with valid integrity
- WHEN Resend does not respond within the timeout
- THEN registration status becomes PaidPendingNotification
- AND HTTP 200 is returned to Mercado Pago

### Requirement: Orphan webhook handling

A webhook whose `external_reference` does not match any registration row is an orphan. The system MUST persist the notification data for audit and MUST return HTTP 200.

#### Scenario: Orphan notification

- GIVEN a webhook with external_reference not found in registrations
- WHEN the webhook is processed
- THEN the orphan data is persisted for audit
- AND HTTP 200 is returned

### Requirement: Closed-tournament distinction

The system MUST distinguish between a late-approval webhook for a recently-closed tournament (flag for review) and a stale-link payment from a long-closed tournament (reject). Both MUST return HTTP 200.

#### Scenario: Late approval for recently-closed tournament

- GIVEN an approved webhook where the tournament closed < 24 hours ago
- WHEN processed
- THEN registration status becomes InReview and HTTP 200 is returned

#### Scenario: Stale-link payment for old tournament

- GIVEN an approved webhook where the tournament closed > 30 days ago
- WHEN processed
- THEN the payment is rejected and HTTP 200 is returned

---

**Out of scope for this change**: Manual resend of notification emails. This is a future admin capability.
