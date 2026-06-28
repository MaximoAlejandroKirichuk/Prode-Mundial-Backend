# Tasks: Add Mercado Pago Back URLs

## Review Workload Forecast

- Estimated changed lines: ~180 production + ~100 test + ~60 config/DI = ~340
- 400-line budget risk: Low
- Chained PRs recommended: No
- Decision needed before apply: No

## Phase 1: Configuration and SDK abstraction

- [x] 1.1 Add `BackUrlsConfig` sub-class and `AutoReturn` property to `MercadoPagoOptions`
- [x] 1.2 Create internal `IPreferenceClient` interface (wraps `PreferenceClient.CreateAsync`)
- [x] 1.3 Create internal `SdkPreferenceClient` that delegates to real `PreferenceClient`
- [x] 1.4 Update `MercadoPagoApi` to accept `IPreferenceClient` via constructor and use it; set `BackUrls` and `AutoReturn` on the `PreferenceRequest`
- [x] 1.5 Verify `MercadoPagoService` production constructor still works (no change needed — constructor chain intact)
- [x] 1.6 `IPreferenceClient` created internally, no DI registration needed
- [x] 1.7 Update `appsettings.json` with `BackUrls` and `AutoReturn` defaults

## Phase 2: Tests

- [x] 2.1 Write unit tests for `MercadoPagoApi` verifying BackUrls and AutoReturn are set on the request (TDD: RED → GREEN → TRIANGULATE → REFACTOR)
- [x] 2.2 Verify existing `MercadoPagoServiceTests` still pass after refactor — 92/92 passing
