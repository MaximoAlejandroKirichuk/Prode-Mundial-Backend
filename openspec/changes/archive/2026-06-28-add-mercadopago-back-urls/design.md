# Design: Add Mercado Pago Back URLs

## Architecture Decision: Configuration location

**Decision**: Add `BackUrls` and `AutoReturn` to the existing
`MercadoPagoOptions` class (bound to `"MercadoPago"` config section).

**Rationale**: These are Mercado Pago preference properties, logically belonging
with the other MP settings. The `AccessLinkOptions` pattern (separate section
for a different concern) is preserved — we don't mix frontend routing into MP
config.

**Alternatives considered**:
- Separate `"MercadoPago:BackUrls"` section: chosen; it nests cleanly.
- Environment-specific configs: already handled by `appsettings.{Environment}.json`.

## Architecture Decision: Testable SDK integration

**Decision**: Introduce an internal `IPreferenceClient` interface wrapping
`PreferenceClient.CreateAsync` so `MercadoPagoApi` can be unit-tested without
calling the live MP API.

**Rationale**: The Mercado Pago SDK's `PreferenceClient` is a concrete class
with no interface. Without a wrapper, tests of `MercadoPagoApi` would either
hit the live API or be impossible. The internal `IMercadoPagoApi` already
follows this pattern for `MercadoPagoService` — we extend it one layer down.

**Class diagram (simplified)**:
```
MercadoPagoService → IMercadoPagoApi → MercadoPagoApi → IPreferenceClient
                                         (sets BackUrls)    └─ SdkPreferenceClient (real SDK)
```

## Configuration shape

```json
"MercadoPago": {
    "AccessToken": "...",
    "UseSandbox": true,
    "BackUrls": {
        "Success": "https://app.example.com/pago/exito",
        "Failure": "https://app.example.com/pago/error",
        "Pending": "https://app.example.com/pago/pendiente"
    },
    "AutoReturn": "approved"
}
```

## Sequence: Preference creation

```
User → FE → POST /api/registrations → CreateRegistrationUseCase
  → MercadoPagoService.CreatePreferenceAsync
    → MercadoPagoApi.CreatePreferenceAsync
      → Build PreferenceRequest with Items, ExternalReference, BackUrls, AutoReturn
      → preferenceClient.CreateAsync(request)  // calls MP API
      → Return PreferenceId + InitPoint
```

## Not in scope

- No new backend endpoint for `/pago/exito` etc. — those are frontend routes.
- No query-param parsing on redirect return — the frontend reads query params
  from the URL Mercado Pago appends.
