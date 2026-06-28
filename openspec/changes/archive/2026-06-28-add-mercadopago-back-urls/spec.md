# add-mercadopago-back-urls Specification

## Purpose

Configure Mercado Pago payment preferences with browser redirect URLs
(`back_urls.success`, `back_urls.failure`, `back_urls.pending`) and
`auto_return = "approved"` so that after payment the user is sent back to the
frontend at the appropriate route.

## Requirements

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
