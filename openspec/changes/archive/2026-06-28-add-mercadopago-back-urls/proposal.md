# Proposal: Add Mercado Pago Back URLs

## Intent

Configure Mercado Pago payment preferences with `back_urls` (success, failure,
pending) and `auto_return = "approved"` so that users are redirected back to the
frontend after completing or abandoning a payment.

## Scope

- Add `BackUrls` and `AutoReturn` configuration to `MercadoPagoOptions`.
- Update `MercadoPagoApi.CreatePreferenceAsync` to set `BackUrls` and `AutoReturn`
  on the `PreferenceRequest`.
- Introduce an internal `IPreferenceClient` abstraction over the Mercado Pago
  SDK `PreferenceClient` to enable unit testing of the request-building logic.
- Add tests verifying the preference request includes the configured URLs.
- Update `appsettings.json` with sensible defaults for development.

The webhook remains the authoritative source for payment confirmation; the
browser redirect is a UX convenience only.

## Out of Scope

- Frontend routes `/pago/exito`, `/pago/error`, `/pago/pendiente` — these live
  in the frontend repo.
- A backend endpoint to serve final payment/registration status after redirect.
- Changing webhook processing logic.

## Rollback

Revert the Git commit. The Mercado Pago preference will simply lack back URLs —
users stay on the Mercado Pago screen after payment. No data corruption risk.
