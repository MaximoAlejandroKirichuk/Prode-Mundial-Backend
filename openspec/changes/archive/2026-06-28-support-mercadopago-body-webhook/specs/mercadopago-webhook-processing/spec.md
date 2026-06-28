# Delta for mercadopago-webhook-processing

## ADDED Requirements

### Requirement: Webhook JSON body acceptance

The webhook endpoint MUST accept Mercado Pago JSON body payloads with fields `type`, `action`, and `data.id`. The system SHALL NOT require query parameters when a valid JSON body supplies the needed fields.

#### Scenario: Full JSON body webhook from Mercado Pago

- GIVEN a POST with JSON body `{"type":"payment","action":"payment.updated","data":{"id":"123456"}}`
- WHEN the webhook is received
- THEN the use case is invoked with topic="payment" and paymentIdStr="123456"

#### Scenario: data.id as integer in JSON body

- GIVEN a JSON body where `data.id` is the integer 789012 (not a string)
- WHEN deserialized
- THEN paymentIdStr is "789012"

#### Scenario: Body with only type and data.id

- GIVEN a JSON body `{"type":"payment","data":{"id":"555"}}` with no action field
- WHEN the webhook is received
- THEN the use case is invoked with topic="payment" and paymentIdStr="555"

### Requirement: Legacy query-param compatibility

The system MUST preserve backward compatibility with callers using query parameters `topic` and `id`. Query-param values SHALL serve as fallback when the corresponding body field is absent or unparseable.

#### Scenario: Query-only webhook without body

- GIVEN query params topic=payment&id=111222 and no JSON body
- WHEN the webhook is received
- THEN the use case is invoked with topic="payment" and paymentIdStr="111222"

#### Scenario: Body with missing type falls back to query

- GIVEN a JSON body `{"action":"payment.updated"}` with no `type` field and query param topic=payment
- WHEN the webhook is received
- THEN topic="payment" is derived from the query param

#### Scenario: Body with missing data.id falls back to query

- GIVEN a JSON body `{"type":"payment"}` with no `data.id` and query param id=999
- WHEN the webhook is received
- THEN paymentIdStr="999" is derived from the query param

### Requirement: Missing-input validation

The system MUST return HTTP 400 when neither body fields nor query parameters supply the required values for processing.

#### Scenario: No body and no query params

- GIVEN a POST with empty body and no query parameters
- WHEN the webhook is received
- THEN HTTP 400 is returned with a validation error

#### Scenario: Malformed JSON body with no query fallback

- GIVEN a POST with unparseable JSON body and no query parameters
- WHEN the webhook is received
- THEN HTTP 400 is returned
