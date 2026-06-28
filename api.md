# API frontend guide

This backend exposes two public endpoints:

- **`GET /api/tournaments/active`** — returns the current active tournament for the landing page.
- **`POST /api/registrations`** — starts the Mercado Pago checkout flow.

## Quick path

1. On page load, call `GET /api/tournaments/active` to get the current tournament.
2. Send `name`, `email`, and `tournamentId` to `POST /api/registrations`.
3. If the request is valid, the API returns a Mercado Pago URL in `paymentUrl`.
4. Redirect the user to that URL.
5. The backend later processes the Mercado Pago webhook server-to-server.
6. After Mercado Pago returns the browser, the frontend should show a status page and treat the webhook as the source of truth.

---

## 1) Active tournament endpoint

### Route

`GET /api/tournaments/active`

### Description

Returns the single active tournament. The frontend should call this on page load to display tournament details (name, price) and obtain the `tournamentId` needed for registration.

### Success response

**Status:** `200 OK`

```json
{
  "tournamentId": "11111111-2222-3333-4444-555555555555",
  "name": "Prode Mundial 2026",
  "priceAmount": 5000,
  "currency": "ARS"
}
```

### Response fields

| Field | Type | Meaning |
|-------|------|---------|
| `tournamentId` | guid | Unique tournament identifier |
| `name` | string | Display name |
| `priceAmount` | decimal | Registration price |
| `currency` | string | ISO 4217 currency code (e.g. `ARS`) |

### Not found response

**Status:** `404 Not Found`

No body. Frontend should display a message like "No hay torneo activo en este momento."

---

## 2) Checkout endpoint

### Route

`POST /api/registrations`

### Request body

```json
{
  "name": "Juan Perez",
  "email": "juan@example.com",
  "tournamentId": "11111111-2222-3333-4444-555555555555"
}
```

### Required fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | Yes | Max 200 chars |
| `email` | string | Yes | Must be a valid email, max 320 chars |
| `tournamentId` | guid | Yes | Comes from the landing |

### Success response

**Status:** `201 Created`

The API also returns a `Location` header pointing to `/api/registrations/{registrationId}`.

```json
{
  "registrationId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "paymentUrl": "https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id=...",
  "isExisting": false
}
```

### Response fields

| Field | Type | Meaning |
|-------|------|---------|
| `registrationId` | guid | Internal registration id |
| `paymentUrl` | string | Mercado Pago checkout URL (`init_point`) |
| `isExisting` | bool | `true` if the API reused a recent pending checkout; frontend should still redirect to `paymentUrl` |

### Mercado Pago browser return

The backend now configures Mercado Pago preference return URLs:

- `success` → frontend route for approved browser return
- `failure` → frontend route for failed or cancelled browser return
- `pending` → frontend route for pending browser return
- `auto_return` → `approved`

Recommended frontend routes:

- `/pago/exito`
- `/pago/error`
- `/pago/pendiente`

Mercado Pago usually returns query params such as:

- `payment_id`
- `status`
- `merchant_order_id`
- `preference_id`

The frontend may read those params for display, but must not treat them as final payment confirmation.

---

## 3) Business rules the frontend must know

### Tournament validation

- The backend rejects the request if `tournamentId` does not exist.
- The backend rejects the request if the tournament is inactive.

### Duplicate policy

The rule is scoped by **(`email`, `tournamentId`)**.

- If there is a **pending** registration created less than 5 minutes ago, the API reuses it and returns the same `paymentUrl` with `isExisting: true`.
- If that email already has a **paid / active** registration for the same tournament, the API blocks the purchase.
- If the previous attempt for that same tournament was **rejected** or **expired**, the API creates a new pending registration and a new Mercado Pago preference.

### Identity continuity

The access flow is tied to the same email used during registration.

The frontend should clearly tell the user:

> After payment, access must be done with the same email used to register.

---

## 4) Error responses

The controller returns `ProblemDetails` for business or validation errors.

### `400 Bad Request`

Used when:

- the JSON body is malformed
- a field type is invalid (for example, `tournamentId` is not a valid GUID format)

### `422 Unprocessable Entity`

Used when:

- required fields are missing
- email format is invalid
- `tournamentId` is missing or empty
- tournament does not exist
- tournament is inactive

Typical titles:

- `Validation Error`
- `Tournament Not Found`
- `Tournament Not Active`

### `409 Conflict`

Used when the same `email` already has a paid registration for the same `tournamentId`.

Typical title:

- `Duplicate Registration`

### `503 Service Unavailable`

Used when the payment provider cannot be used at that moment.

Typical title:

- `Service Unavailable`

### `500 Internal Server Error`

Used for unexpected server-side failures.

---

## 5) Frontend integration behavior

### Recommended UX flow

1. User completes name + email.
2. Landing includes the selected `tournamentId`.
3. Frontend calls `POST /api/registrations`.
4. If success, frontend redirects to `paymentUrl`.
5. Frontend should not try to confirm payment itself; payment confirmation is handled by the backend webhook.
6. When Mercado Pago redirects the browser back to `/pago/exito`, `/pago/error`, or `/pago/pendiente`, the frontend should show a user-friendly state page and explain that final confirmation depends on the backend webhook, not only on the browser return.

If the response returns `isExisting: true`, frontend should keep the same behavior and redirect to the returned `paymentUrl`.

### Recommended frontend messages

- Before redirect: "Vas a ser redirigido a Mercado Pago."
- After form submit error 409: "Ya existe una inscripción paga para este torneo con ese email."
- After form submit error 422: "Revisá los datos ingresados o el torneo seleccionado."
- Reminder: "El acceso al Prode se habilita con el mismo email con el que te registraste."

---

## 6) What the backend does after payment

This part is server-to-server, but frontend should know the outcome model.

- Mercado Pago calls the backend webhook.
- Webhook endpoint: `POST /api/mercadopago/webhook`.
- The checkout browser return is only UX navigation, not authoritative payment confirmation.
- The backend validates the payment and avoids processing duplicates.
- If payment is approved and consistent, the backend marks the registration as paid.
- Then it tries to send the access email.
- If the email send fails, the payment is still preserved and the registration remains pending notification.

---

## 7) Important notes

- The public checkout route is currently **`/api/registrations`**, not `/api/inscripciones`.
- `paymentUrl` is the field the frontend should use for redirect.
- `tournamentId` must come from the landing.
- The backend, not the frontend, is responsible for webhook reconciliation and access email delivery.
- Browser return pages (`/pago/exito`, `/pago/error`, `/pago/pendiente`) are UX routes only. Final payment state belongs to the backend webhook flow.

---

## 8) Example fetch

### Get active tournament

```ts
const response = await fetch("/api/tournaments/active");

if (response.ok) {
  const tournament = await response.json();
  // tournament.tournamentId, tournament.name, tournament.priceAmount, tournament.currency
}

if (response.status === 404) {
  // No active tournament — show appropriate message
}
```

### Register for tournament

```ts
const response = await fetch("/api/registrations", {
  method: "POST",
  headers: {
    "Content-Type": "application/json"
  },
  body: JSON.stringify({
    name: form.name,
    email: form.email,
    tournamentId: selectedTournamentId
  })
});

if (response.ok) {
  const data = await response.json();
  window.location.href = data.paymentUrl;
  return;
}

const error = await response.json();
throw new Error(error.detail ?? "Registration failed");
```
