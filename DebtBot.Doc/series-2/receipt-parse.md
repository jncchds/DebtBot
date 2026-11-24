# Receipt parse and prefill: plan

Status: **draft for the user** (2026-11-24). Plan only in series 2; it gets built in a later series. Written by the orchestrator.

## Goal
On New bill, the user can turn a photo or pasted text of a receipt into a prefilled form: description, date, currency, total with tips, and lines. They then choose people per line, add payments and save as usual. Nothing is saved or finalized by the model.

## Flow
1. New bill shows a "From a receipt" button, but only when the feature is on (see Settings).
2. A panel opens with a photo picker (camera on phones via `accept="image/*" capture`), a drop zone, a "Paste text instead" tab, and the privacy note: *"The receipt is sent to an AI service (OpenRouter, model X) to read it. Don't send receipts with personal data you don't want to share."*
3. The client shrinks the image (longest side 1600 px, JPEG about 0.8, which keeps it under about 1 MB) and posts it to `POST Bills/receipt` (multipart: `image` or `text`).
4. The server calls the model, validates the result, normalises it, and returns a `ReceiptDraft` (below), plus `warnings`.
5. The client fills the form. When the form already has content, it asks "Replace the form?" first. Lines arrive with every bill person ticked, as `LineEditor` does now. Warnings appear above the lines, for example "Lines add up to 41.50 but the total is 45.00 (tips 3.50?)".
6. The user edits, picks people and payments, and saves as a draft or finalizes, exactly as today.

Nothing about the receipt is stored: not the image, not the text, not the raw model output. Logs record only timing, the model, the outcome and token counts.

## Backend
- **`ILlmClient`**, with one method `Task<LlmResult> CompleteJsonAsync(LlmRequest, CancellationToken)`. `OpenRouterClient` implements it through `HttpClient` (typed client, `IHttpClientFactory`) against `{BaseUrl}/chat/completions`:
  - `model`, or `models: [primary, ...fallbacks]` so OpenRouter can fall back itself;
  - `messages`: a system prompt (rules below) and a user message with `{type: "image_url", image_url: {url: "data:image/jpeg;base64,..."}}` or the text;
  - `response_format: {type: "json_schema", json_schema: {name: "receipt", strict: true, schema}}`. For models that only support `json_object`, it falls back to the schema in the prompt and our own validation;
  - `temperature: 0`;
  - headers `Authorization: Bearer <key>`, plus the optional `HTTP-Referer` and `X-Title: DebtBot`.
- **`IReceiptService.ParseAsync(image | text)`** calls the client, validates against the schema (System.Text.Json plus a JSON Schema validator, for example `JsonSchema.Net`), then applies our rules and normalisation, and returns a `ReceiptDraft`.
- **Endpoint** `POST Bills/receipt`:
  - `[Authorize]`, and not allowed for read-only API tokens (it costs money or quota);
  - per-user rate limit (for example 10 per hour and 30 per day, as settings);
  - request size limit of 5 MB; images only as jpeg, png, webp or heic (heic is converted on the client, or refused with a hint);
  - returns 503 `{code: "receiptUnavailable"}` when the feature is off.
- **Feature flag:** `GET Auth/config` (already anonymous config) gets `receiptParse: true|false`, which is true only when `Llm:ApiKey` is set.

### Prompt rules
Read only what is printed. Never invent items. Use the item total of each line (quantity × price), with discounts applied to their lines. Tips, service charge and tax: if they're included in the total, report them separately. The currency is the ISO code if printed, else the symbol. Dates use ISO. If the image isn't a receipt, return `{isReceipt: false}`.

### Schema (model output)
```json
{
  "isReceipt": true,
  "merchant": "Corner Café",
  "date": "2026-11-20T19:42",
  "currency": "EUR",
  "currencySymbol": "€",
  "items": [{ "name": "Borscht", "quantity": "2", "amount": "13.80" }],
  "tips": "3.50",
  "serviceCharge": null,
  "tax": "0",
  "taxIncluded": true,
  "total": "45.00"
}
```
- Amounts and quantities are **strings**, so that a "1.234,50" style or a rounding never passes through a float. Every field except `isReceipt` and `items` is nullable.

### Normalisation (ours, tested)
- **Numbers:** strip spaces and currency signs. If a string has both `.` and `,`, the last one is the decimal separator. A single `,` followed by exactly 2 digits is a decimal. Parse with `decimal` and the invariant culture, and round to the currency's minor units.
- **Currency:** an ISO code if the code is valid. Otherwise map the symbol (€, $, £, ₽, ₸, ₺, ...). An ambiguous `$` uses the user's last bill currency if it's a dollar, else a warning and an empty field. If there's nothing at all, leave it empty and let the user type it.
- **Total with tips:** `total` + `tips` + `serviceCharge` when they're shown outside the total; else `total`. If the items don't sum to the total within 1%, add a warning. Never "fix" the amounts.
- **Description:** the merchant name, plus the date when no time is printed.
- **Limits:** at most 100 items, names cut to the line description length, negative amounts kept only as discount lines.
- `isReceipt: false`, or invalid JSON after one retry, becomes 422 `{code: "notAReceipt" | "unreadable"}`.

### `ReceiptDraft` (API to UI)
`{ description, date?, currencyCode?, totalWithTips?, lines: [{description, subtotal}], warnings: [code + params], model }`. These are the New bill form's own fields, so prefill is a plain mapping.

## Settings
| Key | Default | Note |
|---|---|---|
| `Llm:BaseUrl` | `https://openrouter.ai/api/v1` | any OpenAI-compatible API (a local Ollama or vLLM works too) |
| `Llm:Model` | `nvidia/nemotron-3.5-super-vl:free` | checked 2026-11-24; must take images and structured output |
| `Llm:FallbackModels` | `google/gemma-4-31b-it:free` | sent as `models[]` |
| `Llm:ApiKey` | (empty) | secret: environment variable or user secrets, never in appsettings; empty turns the feature off |
| `Llm:TimeoutSeconds` | 45 | |
| `Llm:UserHourlyLimit` / `UserDailyLimit` | 10 / 30 | ours |

**Free models on 2026-11-24** (images in, `:free`): `nvidia/nemotron-3.5-super-vl:free` (structured outputs), `inclusionai/ling-3.2-flash:free` (structured outputs), `google/gemma-4-31b-it:free` and `gemma-4-26b-a4b-it:free` (`json_object` only), and the `openrouter/free` router. The list changes often, so the model is a setting and the build task re-checks it.

**OpenRouter free limits** (from their docs; recheck when building): about 20 requests a minute; a daily cap of 50 a day for accounts that have never bought credits, and 1000 a day once at least $10 of credits has been bought. Free models may log prompts and use them for training, depending on the provider. That is what the privacy note is for, and the reason the build task should check the provider's data policy setting in the OpenRouter account.

## Errors and fallback
The UI shows one clear message and keeps the form as it was, for:
- timeout;
- OpenRouter 429 or 402 ("The free AI service is busy or out of quota. Try later or fill the bill in by hand.");
- 5xx;
- invalid JSON;
- not a receipt.

The client retries once on 5xx or timeout; the server never retries more than once. Manual entry is always there.

## Tests (no real API in CI)
- `FakeLlmClient` returns fixture JSON chosen by the test, and there are recorded fixtures under `DebtBot.Tests/Fixtures/receipts/` (a cafe in EUR, a supermarket with a discount, "1.234,50" style, `$` ambiguity, tips outside the total, not a receipt, broken JSON).
- Unit tests cover normalisation and validation; endpoint tests (with the fake) cover the feature flag, the rate limit, the size limit and the read-only token refusal.
- `OpenRouterClient` is tested against a stub `HttpMessageHandler` (the request shape, headers, the `models` array, error mapping).
- vitest covers mapping the draft into the form, replace confirmation and warnings.
- One manual run with a real key is recorded in the build task's report. It is never in CI.

## The bot (later, optional)
A photo sent to the bot runs the same `IReceiptService`, then the bot replies with a prefilled **draft** link (`/bills/new?receipt=<token>`, kept server side for 15 minutes) instead of making a bill. That is the user's call, for after the web flow.

## Build split (for the later series)
1. Backend: `ILlmClient`, OpenRouter client, receipt service, schema, normalisation, endpoint, settings, feature flag, fake and tests.
2. UI: mockups first (button, panel, error and warning states, prefilled form), then the build.
3. End-to-end check with the fake, plus one real manual run.

## Open for the user
- Is the privacy note wording right, and should it be shown every time or once per user?
- The limits (10 an hour, 30 a day per user): fine?
- Should text paste be part of it, given that the existing "Paste text" bill format already exists? It could stay photo only.
- Should the bot get it later, or never?
