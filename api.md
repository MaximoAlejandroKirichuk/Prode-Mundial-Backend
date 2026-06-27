# API frontend guide

This backend currently exposes the registration checkout endpoint at **`POST /api/registrations`**.

It is the endpoint the Astro landing page should call to start the Mercado Pago flow.

## Quick path

1. Send `name`, `email`, and `tournamentId` to `POST /api/registrations`.
2. If the request is valid, the API returns a Mercado Pago URL in `paymentUrl`.
3. Redirect the user to that URL.
4. The backend later processes the Mercado Pago webhook server-to-server.

---

## 1) Checkout endpoint

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
| `email` | string | Yes | Must be a valid email |
| `tournamentId` | guid | Yes | Comes from the landing |

### Success response

**Status:** `201 Created`

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
| `isExisting` | bool | `true` if the API reused a recent pending checkout |

---

## 2) Business rules the frontend must know

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

## 3) Error responses

The controller returns `ProblemDetails` for business or validation errors.

### `422 Unprocessable Entity`

Used when:

- required fields are missing
- email format is invalid
- `tournamentId` is missing or invalid
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

---

## 4) Frontend integration behavior

### Recommended UX flow

1. User completes name + email.
2. Landing includes the selected `tournamentId`.
3. Frontend calls `POST /api/registrations`.
4. If success, frontend redirects to `paymentUrl`.
5. Frontend should not try to confirm payment itself; payment confirmation is handled by the backend webhook.

### Recommended frontend messages

- Before redirect: "Vas a ser redirigido a Mercado Pago."
- After form submit error 409: "Ya existe una inscripción paga para este torneo con ese email."
- After form submit error 422: "Revisá los datos ingresados o el torneo seleccionado."
- Reminder: "El acceso al Prode se habilita con el mismo email con el que te registraste."

---

## 5) What the backend does after payment

This part is server-to-server, but frontend should know the outcome model.

- Mercado Pago calls the backend webhook.
- The backend validates the payment and avoids processing duplicates.
- If payment is approved and consistent, the backend marks the registration as paid.
- Then it tries to send the access email.
- If the email send fails, the payment is still preserved and the registration remains pending notification.

---

## 6) Important notes

- The public checkout route is currently **`/api/registrations`**, not `/api/inscripciones`.
- `paymentUrl` is the field the frontend should use for redirect.
- `tournamentId` must come from the landing.
- The backend, not the frontend, is responsible for webhook reconciliation and access email delivery.

---

## 7) Example fetch

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
