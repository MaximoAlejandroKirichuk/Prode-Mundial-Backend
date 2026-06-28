# Delta for registration-checkout

## ADDED Requirements

### Requirement: Checkout request MUST include tournament_id

The system MUST accept `name`, `email`, and `tournament_id` in the checkout request. The system MUST reject requests with missing or invalid fields using HTTP 422.

#### Scenario: Valid checkout payload

- GIVEN a valid name, email, and active tournament_id
- WHEN POST /api/registrations is called
- THEN a pending registration is created and the response includes an init_point URL

#### Scenario: Missing tournament_id

- GIVEN a request without tournament_id
- WHEN POST /api/registrations is called
- THEN HTTP 422 is returned with a validation error

### Requirement: Tournament validation MUST reject invalid or inactive tournaments

The system MUST validate that `tournament_id` references an existing, active tournament. Requests referencing nonexistent or inactive tournaments MUST be rejected with HTTP 422.

#### Scenario: Nonexistent tournament

- GIVEN a tournament_id that does not exist in the database
- WHEN checkout is requested
- THEN HTTP 422 is returned

#### Scenario: Inactive tournament

- GIVEN a tournament_id referencing a tournament that is not active
- WHEN checkout is requested
- THEN HTTP 422 is returned

### Requirement: Duplicate checkout policy scoped by (email, tournament_id)

The system MUST enforce duplicate rules per (email, tournament_id) pair:

| Rule | Condition | Behavior |
|------|-----------|----------|
| Reuse | Pending < 5 min ago | Return existing init_point |
| Block | Paid exists | HTTP 409 |
| Retry | Rejected or pending expired | Create new pending row |

(Previously: duplicate guard was global by email only, without tournament scope.)

#### Scenario: Reuse recent pending

- GIVEN a pending registration for (email, tournament_id) created 2 minutes ago
- WHEN checkout is requested for the same pair
- THEN the existing init_point is returned and no new row is created

#### Scenario: Block duplicate paid

- GIVEN a Paid registration for (email, tournament_id)
- WHEN checkout is requested for the same pair
- THEN HTTP 409 is returned

#### Scenario: Retry after rejection

- GIVEN a Rejected registration for (email, tournament_id)
- WHEN checkout is requested for the same pair
- THEN a new pending registration is created with a fresh MP preference

### Requirement: Mercado Pago preference creation with external_reference

The system MUST create a Mercado Pago preference for each new pending registration. The preference MUST set `external_reference` to the registration ID. The response MUST include the `init_point` URL.

(Previously: placeholder returned NotImplementedException.)

#### Scenario: New preference created

- GIVEN a valid checkout request creates a new pending registration
- WHEN the MP preference is generated
- THEN external_reference equals the registration ID
- AND the response includes the init_point URL

### Requirement: Back URLs in preference request

The system MUST include `back_urls.success`, `back_urls.failure`, and
`back_urls.pending` in every Mercado Pago `PreferenceRequest` created during
registration checkout. The values SHALL be read from application configuration.

#### Scenario: All three back URLs populated

- GIVEN configuration defines `BackUrls.Success = "https://app.example.com/pago/exito"`,
  `BackUrls.Failure = "https://app.example.com/pago/error"`, and
  `BackUrls.Pending = "https://app.example.com/pago/pendiente"`
- WHEN a payment preference is created
- THEN the `PreferenceRequest.BackUrls.Success` is the configured success URL
- AND `BackUrls.Failure` is the configured failure URL
- AND `BackUrls.Pending` is the configured pending URL

#### Scenario: Auto return after approved payment

- GIVEN `AutoReturn` is configured as `"approved"`
- WHEN a payment preference is created
- THEN `PreferenceRequest.AutoReturn` is set to `"approved"`

#### Scenario: Null BackUrls when not configured (graceful)

- GIVEN configuration does NOT define any `BackUrls` section
- WHEN a payment preference is created
- THEN `PreferenceRequest.BackUrls` is null
- AND the preference is still created successfully

### Requirement: Webhook remains authoritative

The system MUST continue to treat the Mercado Pago webhook notification as the
single source of truth for payment confirmation. The browser redirect via
`back_urls` is a UX convenience and MUST NOT trigger any payment state change.

#### Scenario: Webhook still processes payment independently of redirect

- GIVEN a user paid and was redirected to `/pago/exito`
- WHEN the webhook for that payment arrives
- THEN the registration transitions through the normal Paid → Notified flow
- AND the redirect itself does not alter registration state
