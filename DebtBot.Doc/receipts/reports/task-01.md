# T1 report: receipt backend

- **Branch:** `task/01-receipt-backend`, based on the local `receipts` at `cb1879a` (before the build-versioning merge, PR #37; the Dockerfile isn't touched, so no rebase is needed).
- **Commits:**
  - `86c2b67`: the backend and the tests (pushed as WIP when the session hit its usage limit);
  - `9af7c30`: a multipart body over the limit answers 413 `tooLarge`, not MVC's 400;
  - the last commit: docs (`api.md`, `README.md`), the runtime check script, and this report.

## Recheck (2026-11-27)
- **Free models with image input**, from OpenRouter's public `GET /api/v1/models` (476 models):
  - `nvidia/nemotron-3.5-super-vl:free` (structured outputs);
  - `particle-ai/particle-1:free` (structured outputs; new, October 2026);
  - `google/gemma-4-31b-it:free` and `google/gemma-4-26b-a4b-it:free` (`response_format` only, so `json_object`);
  - `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free` (no `response_format`);
  - the `openrouter/free` router.
  - `inclusionai/ling-3.2-flash` no longer has a `:free` variant.
- **Defaults kept:** `Model` `nvidia/nemotron-3.5-super-vl:free`, `FallbackModels` `google/gemma-4-31b-it:free`. Both are still free and take images, and the primary has structured outputs. Particle-1 is too new to trust as the default; it's one setting away.
- **Free limits** (OpenRouter docs, "Limits"): 20 requests a minute; 50 a day with under 10 credits ever bought, 1000 a day with at least 10. The account has $10, so the 1000 applies. 429 is "rate limited" and 402 is out of credits (`error.metadata.limit_source`). Both map to `busy`.
- **Structured outputs and fallbacks:** the docs say `provider.require_parameters: true` limits routing to endpoints that support the schema. We **don't** set it, because it would rule out the gemma fallback. Instead the schema is always in the system prompt too, and our own validation decides. A fallback that ignores `json_schema` still answers in shape.
- All of this is in the `LlmConfiguration` doc comments too.
- **Not checked:** the account's data policy setting needs the account, so it's left for T4.

## What was built
All of it is in `DebtBot/Receipts/` unless noted.
- **Settings** (`LlmConfiguration`, section `DebtBot:Llm`, environment variables `DebtBot_DebtBot__Llm__*`):
  - `Provider` (`OpenRouter` | `Fake`), `BaseUrl`, `Model`, `FallbackModels` (comma-separated), `ApiKey`, `ResponseFormat` (`json_schema` | `json_object`), `Referer` (optional `HTTP-Referer`), `TimeoutSeconds` 45, `UserHourlyLimit` 10, `UserDailyLimit` 30, `MaxImageBytes` 5 MB;
  - `appsettings.json` has the non-secret defaults with an empty `ApiKey`. `docker-compose.yml` has a commented `DebtBot_DebtBot__Llm__ApiKey=` line with a note.
- **`ILlmClient`** (`Llm.cs`): `CompleteJsonAsync(LlmRequest)` → `LlmResult(Outcome, Model, Json, PromptTokens, CompletionTokens, StatusCode)`. The outcomes are `Ok`, `Timeout`, `Busy` (429/402), `Upstream` (5xx, a 200 without an answer, a network error), `InvalidJson` and `Failed` (other 4xx).
- **`OpenRouterClient`**, a typed `HttpClient` against `{BaseUrl}/chat/completions`:
  - the body has `model`, `models: [primary, ...fallbacks]` (only when there are fallbacks), a system message, and a user message with text plus the image as a `data:` URL;
  - `response_format` is `json_schema` (`name: receipt`, `strict: true`) or `json_object`, with `temperature: 0`;
  - headers: `Authorization: Bearer`, `X-Title: DebtBot`, and `HTTP-Referer` when set;
  - our timeout comes from the settings (a linked token). The caller's own cancellation isn't counted as a timeout;
  - an `error` object inside a 200 is mapped by its code. The content may come in a ```` ```json ```` fence or with chatter around the object; both are tolerated;
  - it logs one line per call: outcome, status, model, ms and tokens. Never the key, the image or the output (tested).
- **Schema** (`ReceiptSchema.cs`):
  - the strict schema sent to the model is the spec's, plus `tipsIncluded` and `serviceChargeIncluded`. The spec's schema had no way to tell tips inside the total from tips outside it;
  - the answer is validated with **JsonSchema.Net 9.7.0** against a lenient copy: only `isReceipt` and `items` are required, amounts may also be numbers (read from their JSON text, never through a float), and extra fields are allowed;
  - the prompt has the spec's rules plus the strict schema, for `json_object` models.
- **`IReceiptService.ParseAsync(userId, ReceiptInput(image, fileName))`**, in this order:
  1. the feature is on;
  2. the image is there, at most 5 MB, and JPEG, PNG or WebP **by content** (magic bytes; HEIC/HEIF/AVIF get `heicNotSupported`);
  3. the user's limits;
  4. the model, **at most twice**: one retry on invalid JSON, an answer off the schema, `Upstream` or `Timeout`. `Busy`, `Failed` and not-a-receipt aren't retried;
  5. normalisation, the text, and the draft store.
  - It returns `ReceiptParseResult(Draft?, Error?, Limit?)`. `ReceiptErrors` gives each error's code, status and English message, so the bot (T2) can reuse them.
- **Normalisation** (`ReceiptNormalizer.cs`):
  - **Numbers:** spaces (including non-breaking ones), apostrophes, signs and letters are dropped. With both separators, the last one is the decimal. A single `,` with 1–2 digits after it is a decimal comma (the spec says exactly 2; `12,5` reads as 12.5). Other commas, and several dots, group thousands. A leading or trailing minus, or parentheses, mean negative. Amounts are rounded to the currency's minor units (0: JPY, KRW, VND…; 3: KWD, BHD…; else 2).
  - **Currency:** a valid ISO 4217 code, else the sign. An ambiguous sign (`$`, `¥`, `kr`) takes the user's latest bill currency when it's one of the sign's meanings (`currencyFromLastBill`), else it stays empty with `currencyAmbiguous`.
  - **Totals:** `totalWithTips` = total, plus tips or service charge when they're printed outside it (`tipsAdded`, `serviceChargeAdded`). The lines are compared with the total less the extras printed inside it, and less the tax when it's on top of the items; off by more than 1% gives `linesTotalMismatch`. Amounts are never "fixed".
  - **Description:** the merchant (or "Receipt"), plus ` yyyy-MM-dd` when only a date is printed (then `date` is 12:00 UTC). A printed time is kept as printed, treated as UTC.
  - **Limits:** at most 100 lines, names on one line and cut to 200 (the form's limit), `×2` added for quantities other than 1, `Item N` for empty names, unreadable amounts skipped. Negative lines are kept as discount lines (`discountLine`). Negative total, tips, service charge or tax are ignored (`negativeAmountIgnored`).
- **Text builder** (`ReceiptTextBuilder.cs`):
  - it writes the description, the total and currency (`XXX`, ISO's "no currency", when unknown), the payment `<total> <sender>`, and each line as `<name>`, `<subtotal>`, `1 <sender>`;
  - the sender is their Telegram username as stored (usually `@name`; some old rows lack the `@`, and the parser matches the stored value), else their Telegram id;
  - the output parses back through `ParserService` to the same lines and amounts, for every fixture.
- **Receipt drafts** (`ReceiptDraftStore`, **`IMemoryCache`**): `Add(owner, draft)` sets the id and `ExpiresAt` (15 minutes), and `Get(owner, id)` gives null for anyone else, an unknown id, or after expiry (on the `TimeProvider` clock).
  - Why memory: a draft is short-lived, can be made again, and is only read by this one process (the web prefill and the bot's button). A table would add a migration and a purge job. A restart forgets drafts, which is fine for 15 minutes.
- **Per-user limiter** (`IReceiptRateLimiter.TryAcquire(userId)`, a singleton): rolling hour and day windows on the `TimeProvider`, in memory. It answers `(Allowed, Window, RetryAfter)`. It's a service, not middleware, so T2's bot handler goes through it by calling `ParseAsync`.
- **Endpoints** (`Controllers/BillReceiptController.cs`, route `api/v1/Bills/receipt`):
  - `POST` reads the multipart form itself. `[DisableFormValueModelBinding]` (`Crutch/`) stops `[ApiController]` from reading the form first and turning the multipart limit into a 400. It has `[RequestSizeLimit]` and `[RequestFormLimits]` of 5 MB + 64 KB, and a `Content-Length` check;
  - `GET {id}` returns the owner's draft, else 404 `{code: notFound}`;
  - Swagger shows the upload form (`ImageUploadRequestOperationFilter`).
- **Feature flag:** `ReceiptFeature` (a singleton) is on with an `ApiKey`, or with `Provider=Fake` **only in Development**. `GET auth/config` gains `receiptParse` and `receiptModel` (null when off). The app logs the state at start-up.
- **Profile setting:**
  - the enum `ReceiptOpenIn { Form, Text }` (in `DebtBot.Models.Enums`), stored as `User.ReceiptOpenIn` (int, default Form). Migration `20261127215906_ReceiptOpenIn` was made with dotnet-ef 11;
  - `GET profile` has `receiptOpenIn`. `PUT profile` takes `receiptOpenIn?`; null or left out keeps the value, so today's Profile form doesn't reset it, and an undefined value is 400;
  - the brief's "existing own-profile endpoint" is `profile` (`ProfileController`). `Users/Own` was removed in the web-ui series.
- **`FakeLlmClient`** (in the app):
  - with `Llm:Provider=Fake` it answers with the fixture whose name is in the upload's file name, else `cafe-eur`;
  - the hints `busy`, `timeout` and `upstream` give those errors;
  - tests can queue answers (`Enqueue`, `EnqueueFixture`) and read `Requests`;
  - the fixtures are `DebtBot.Tests/Fixtures/receipts/*.json`, embedded in the app by `DebtBot.csproj`, so the app and the tests use the same files (see their `README.md`). The default `Provider` is `OpenRouter`, so the fake is never on by default, and never outside Development.

## Endpoints
New: `POST Bills/receipt` and `GET Bills/receipt/{id}`. Changed: `GET auth/config` gains `receiptParse` and `receiptModel`; `GET/PUT profile` gain `receiptOpenIn`. Everything is in `web-ui/api.md`, "Receipts": shapes, warning codes, error codes, limits and retries. `README.md` has "Receipts (optional)". No pages (no UI in T1).

**Error codes:**

| Status | Code |
|---|---|
| 400 | `noImage` |
| 413 | `tooLarge` |
| 415 | `unsupportedType`, `heicNotSupported` |
| 422 | `notAReceipt`, `unreadable` |
| 429 | `rateLimited` (+ `window`, `retryAfterSeconds`, `Retry-After`) |
| 502 | `upstreamError` |
| 503 | `receiptUnavailable`, `busy` |
| 504 | `timeout` |

A read-only token gets the global 403 `{message}` from the scope middleware.

## Tests
**.NET: 360 passed, 0 failed** (231 before, plus 129). The fixtures are under `DebtBot.Tests/Fixtures/receipts/`: `cafe-eur`, `supermarket-discount`, `decimal-comma`, `dollar-ambiguous`, `tips-outside`, `not-a-receipt` and `broken-json`.
- **`ReceiptNormalizerTests`:**
  - 19 number formats and 6 unreadable ones, minor units (JPY, KWD), and a float-looking JSON number;
  - the currency: ISO, signs, `руб`, an ambiguous `$` with and without a dollar last bill, `¥`, none;
  - each fixture's draft and warnings;
  - tips inside and outside the total, the 1% mismatch and the within-1% case, no total, negative extras;
  - 100 items, names (cut, one line, empty, unreadable amounts), dates;
  - the schema: fixtures valid, 6 invalid shapes, the lenient extras, and strict mode's "every property required";
  - the text: a round trip through `ParserService` for all 5 receipt fixtures, the cafe's exact text, no currency or total, and the sender rule.
- **`OpenRouterClientTests`** (a stub `HttpMessageHandler`):
  - the request: URL, method, `Authorization`, `X-Title`, `HTTP-Referer`, `model` and `models`, messages, the data URL, the strict schema, `temperature`;
  - a single model sends no `models`, and a custom base URL works;
  - `json_object` mode;
  - an answer gives the JSON, model and tokens; fences and chatter are tolerated; non-objects are invalid JSON;
  - 8 statuses mapped, and 7 error bodies inside a 200;
  - the timeout (1 s), the caller's cancellation, a network error;
  - the log line has no key, image or output.
- **`ReceiptEndpointTests`** (the real pipeline over SQLite, with the fake):
  - config on, off without a key, and on with a key (the real `OpenRouterClient` registered); the fake is never on outside Development and never the default;
  - 401; a read-only token gets 403 and the model isn't called; a read-and-write token works;
  - the draft fields, the text and what the model got; warnings as codes and params; the `$` from the last bill (CAD) versus none; the Telegram id as the stand-in for a user without a username;
  - drafts: the owner, a read-only token reading, another user 404, unknown 404, anonymous 401, expiry at 15 minutes (14 → 200, 15 → 404), no image kept;
  - limits: hourly and daily with `Retry-After`; another user unaffected; the same user's token shares the limit; refused calls don't reach the model; the limiter as a service;
  - 5 MB passes, 5 MB + 1 is 413, a 6 MB request is 413; PNG, WebP and JPEG by content; HEIC with the hint; GIF, no file, the wrong field and a JSON body;
  - `notAReceipt` (no retry); `unreadable` after exactly 2 calls; an off-schema answer retried; the retry succeeding; `upstreamError` and `timeout` after exactly 2 calls; `busy` and `Failed` not retried; `json_object` mode as a setting;
  - the Profile setting: Form → Text, kept when left out, back to Form, 400 for 7.

## Runtime check (docker compose, built from this branch)
- **The stack:** the e2e stack (`docker-compose.yml`, the override with `ASPNETCORE_ENVIRONMENT=Development`, and `web-ui/e2e/compose.e2e.yml`), plus a throwaway file with `Llm__Provider=Fake`, `UserHourlyLimit=3` and `UserDailyLimit=5`.
- **`reports/task-01/scripts/t1check.mjs`: 19/19 passed** on a fresh database (the second run; the first stopped at the 6 MB step, see below):
  - the migration is applied, and `Users.ReceiptOpenIn` exists;
  - `GET auth/config` gives `receiptParse: true` and `receiptModel: "fake/receipt-fixtures"`;
  - anonymous gets 401. With a JWT: the cafe draft (EUR, 39.50, 4 lines). Its text starts `Corner Café / 39.50 / EUR / 39.50 @telegram_username` and parses on `POST Bills/text/preview` (valid, 4 lines, 39.50);
  - `GET Bills/receipt/{id}` gives 200 for the owner and 404 `notFound` for Boris;
  - a read-only `dbt_` token gets **403**; a read-and-write token gets the supermarket draft with `discountLine`;
  - the 3rd use in the hour works (tips added, 35.00). The **4th is 429** `rateLimited` (`window: hour`, `Retry-After: 3600`), and the read-and-write token is limited too;
  - as Boris: `notAReceipt` 422, `heicNotSupported` 415, `busy` 503, 5 MB + 1 byte 413, a 6 MB request 413 `tooLarge`;
  - Profile `receiptOpenIn` goes Form → Text and is kept when left out.
- **The 6 MB step:** the first run stopped there. The server answers from `Content-Length` and closes the connection, and Node's `fetch` was still writing (EPIPE). curl reads the early answer, with and without `Expect: 100-continue`: 413 `{code: tooLarge}`. The script uses curl for that step. A browser posting more than 5 MB may see a network error instead of the 413. The web client shrinks images to about 1 MB first (spec), so T3 can treat a network error on upload like `tooLarge`.
- **Also checked:**
  - the app log has the fake-provider warning and the migration, and no image data or receipt content;
  - Swagger lists both endpoints, with the `multipart/form-data` `image` field.
- **Clean-up:** the stack is down with its volumes. The image is removed (it was tagged `jncchds/debtbot:latest`, which replaced an earlier local build with that tag; earlier tasks removed theirs the same way). The build cache is pruned (4.3 GB), the base images are gone with it, and `dotnet build-server shutdown` was run. Disk: 6.6 GB free afterwards (2.6 GB at the lowest).
- **Only compiled and unit-tested, not run against a real API:** `OpenRouterClient`. There is no key before T4.

## Decisions
- **Drafts in memory** (`IMemoryCache`), see above. The **limits are in memory** too: a restart resets them. They're per process; there's one app container.
- **Schema additions:** `tipsIncluded` and `serviceChargeIncluded`. When the model leaves them null, tips are taken as inside the total (nothing is added, and the mismatch check subtracts them).
- **Negative lines are kept as discount lines with a warning**, not merged into the line above. A whole-receipt discount belongs to no line, and merging would "fix" amounts. But `BillRules` refuses lines at or below zero, so the form can't save a draft with one as it is (see "Left open").
- **The image type is checked by content**, not by the declared type: phones and browsers mislabel. HEIC is refused with a hint, not converted.
- **What counts toward the limits:** every call that reaches the model, failed ones included (they cost quota). Calls refused earlier don't count.
- **Status codes:** `busy` and `receiptUnavailable` are 503, which keeps 429 for our own per-user limit; `upstreamError` is 502; `timeout` is 504. Errors are `{code, message}`, plus the 429's `window` and `retryAfterSeconds`.
- **The fake runs only in Development**, whatever the setting says. In Production `Provider=Fake` turns receipts off, with a warning.
- **`XXX`** stands in the text when the currency is unknown. It's ISO's "no currency" code and parses, and it isn't a real currency, so it has to be changed.
- **Not done:** `require_parameters` (see "Recheck").

## For T2 (bot) and T3 (UI)
- **Entry point:** `IReceiptService.ParseAsync(userId, new ReceiptInput(bytes, fileName), ct)` (scoped). It checks the image, the limits, the model, the retry and normalisation, and stores the draft.
  - On success `result.Draft` has `Id`, `Text` (with the user as the stand-in) and `Warnings`.
  - On failure `result.Error` is a `ReceiptError`: use `ReceiptErrors.Message(error)` for the bot's text and `ReceiptErrors.Code(error)` for the code. For `RateLimited`, `result.Limit` has `Window` and `RetryAfter`.
  - `IReceiptService.IsEnabled` says whether to handle photos at all.
- **The bot** gets the photo with `getFile` into memory and passes the bytes. Telegram photos are JPEG. A document upload may be anything; the service checks by content.
- **Draft URL:** `/bills/new?receipt=<id>`. The web reads it with `GET api/v1/Bills/receipt/{id}`, owner only, for 15 minutes; after that, 404 `notFound` ("This receipt has expired or isn't yours.").
- **Fake provider for T3 and T4:** set `DebtBot_DebtBot__Llm__Provider=Fake` on a Development stack (the compose override already sets Development). The upload's file name picks the fixture: `cafe-eur`, `supermarket-discount`, `decimal-comma`, `dollar-ambiguous`, `tips-outside`, `not-a-receipt`, `broken-json`; `busy`, `timeout` and `upstream` give errors. Keep the file name when shrinking the image on the client, or the default cafe answers. `receiptModel` is then `fake/receipt-fixtures`.
- **Warning codes** for the UI to word: `linesTotalMismatch`, `currencyAmbiguous`, `currencyFromLastBill`, `currencyMissing`, `totalMissing`, `tipsAdded`, `serviceChargeAdded`, `discountLine`, `negativeAmountIgnored`, `itemSkipped`, `tooManyItems`, `nameTruncated`, `dateUnreadable`. Their params are in `api.md`, and amounts in params are invariant strings.
- **The Profile setting** is `receiptOpenIn` on `GET/PUT profile`. Send it only from the new control; the existing form can leave it out.
- **`receiptModel`** in `GET auth/config` is the "model X" for the privacy note.

## Left open
- **Discount lines:** T3 (and the bot's text in T2) has to handle a `discountLine`: spread it over the lines, add it to the line above, or let the user remove it. The form refuses lines at or below zero as it stands.
- **For T4:**
  - the one real run with a key: the model's actual answers, the fallback, and `json_schema` on gemma;
  - the data policy setting in the OpenRouter account.
- `particle-ai/particle-1:free` could replace the gemma fallback if it reads receipts well.
- The limiter and the drafts are per process. Running more than one app instance would need a shared store.
