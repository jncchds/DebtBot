Task T1 (receipt backend) of the DebtBot receipts series. An orchestrator session runs the series one task at a time, with one session per task.

First, read `/mnt/git/DebtBot/DebtBot.Doc/receipts/briefs/common.md` and follow it throughout. To read it before your worktree exists, use `git -C /mnt/git/DebtBot show receipts:DebtBot.Doc/receipts/briefs/common.md`.

- Worktree: `/mnt/git/DebtBot/.claude/worktrees/task-01`
- Branch: `task/01-receipt-backend`, based on the local `receipts` branch.
- Read:
  - **`DebtBot.Doc/series-2/receipt-parse.md`, all of it: it is the spec.** Its "Backend", "Text format", "Settings", "Errors and fallback" and "Tests" sections are this task;
  - `DebtBot.Doc/receipts/plan.md` ("User decisions");
  - `DebtBot.Doc/series-2/reports/task-02.md` (API tokens: the read-only scope, `[JwtOnly]`, the per-token rate limiter) and `task-01.md` (the New bill form fields);
  - `DebtBot.Doc/web-ui/api.md`.

## Goal
The server can turn a receipt photo into a `ReceiptDraft` (the New bill form's fields plus the same bill in the bill text format), through `POST Bills/receipt`, with the limits, settings and feature flag from the spec. Everything is tested with a fake LLM client. There is **no real API key** in this task (common.md).

## What exists
- Settings live in `DebtBotConfiguration` (section `DebtBot`, environment variables `DebtBot_DebtBot__…`). Put the LLM settings there as `DebtBot:Llm:*` (`LlmConfiguration`); the spec's `Llm:*` names are short for these.
- `ParserService` parses the bill text format (the bot's and Paste text's). `Program.cs` has the rate limiters (`auth`, per-token). `AuthController` serves `GET Auth/config`. `ProfileController` and `UsersController` hold the own-user endpoints.

## Scope
1. **Recheck first:** the OpenRouter free models that take images and structured output, and the free limits, as of today. Pick the default `Model` and `FallbackModels` from that (the spec's are the starting point). Record what you found, with the date, in the report and in the settings doc comments.
2. **`ILlmClient`** and **`OpenRouterClient`** (typed `HttpClient`), exactly as in the spec's "Backend":
   - `models: [primary, ...fallbacks]`, the image as a data URL, `response_format` json_schema strict, falling back to the schema in the prompt plus our validation for `json_object`-only models; `temperature: 0`; headers `Authorization`, `HTTP-Referer` (optional) and `X-Title: DebtBot`;
   - the timeout from settings; errors mapped to typed outcomes (timeout, busy or out of quota for 429/402, upstream 5xx, invalid JSON);
   - never log the key, the image or the raw output (only timing, model, outcome and token counts).
3. **`IReceiptService.ParseAsync`**: the prompt rules, the schema validation (a JSON Schema validator such as `JsonSchema.Net`, plus System.Text.Json), one retry on invalid JSON or 5xx/timeout (never more), and the **normalisation** from the spec: numbers (both separators, `1.234,50`, minor units), currency (ISO, symbols, the ambiguous `$` via the user's last bill currency), total with tips, description, item limits, discount lines, and the warnings (codes plus params, for example lines not summing to the total within 1%). Never "fix" amounts.
4. **Text builder:** builds the bill text from a `ReceiptDraft`, with the sender as the stand-in payer and line participant (spec, "Text format"). It must parse back through `ParserService` to the same lines and amounts.
5. **Receipt drafts:** a parsed `ReceiptDraft` (never the image) kept 15 minutes for its owner only, with an id; `GET Bills/receipt/{id}` returns it to its owner and 404 to anyone else or after expiry. Your choice whether it's memory (`IMemoryCache`) or a table; say which and why. The bot (T2) and the web prefill (T3) both use it.
6. **`POST Bills/receipt`** (multipart `image`): `[Authorize]`; refused for read-only API tokens (the existing scope check covers POST, so add a test for it); per-user limits of 10 an hour and 30 a day, as settings, applied per user whether the call comes from the web, a token or (later) the bot, so the limiter must be callable from a service and not only as endpoint middleware; a 5 MB request limit; jpeg, png and webp (heic refused with a hint code); 503 `{code: "receiptUnavailable"}` when the feature is off; the error codes from the spec (`notAReceipt`, `unreadable`, busy or quota, timeout, too large, rate limited) as JSON `{code}` with the right status. It returns the `ReceiptDraft` with its `id`, `warnings`, `model` and `text`.
7. **Feature flag:** `GET Auth/config` gets `receiptParse`, true only when `Llm:ApiKey` is set (plus `receiptModel` for the privacy note's "model X", if that's useful to the UI).
8. **Profile setting** "Receipts open in: Form / Text": stored on the user (an enum column, default Form; migration made with `dotnet ef`, see common.md), read and set through the existing own-profile endpoint. T3 builds the UI for it.
9. **Config docs:** `appsettings.json` gets the non-secret `Llm` defaults with an empty `ApiKey`; `docker-compose.yml` gets a commented `DebtBot_DebtBot__Llm__ApiKey=` line with a one-line note, like the bot token's.
10. **`FakeLlmClient`** for tests, and a way to run the app with it (for example `Llm:Provider=Fake` in Development, which returns a fixture by a hint, so the e2e in T4 and the UI in T3 can use it without a key). The fake must never be the default in Production.

## Tests
xUnit, with the fixtures under `DebtBot.Tests/Fixtures/receipts/` listed in the spec (a cafe in EUR, a supermarket with a discount, `1.234,50`, the `$` ambiguity, tips outside the total, not a receipt, broken JSON):
- normalisation and validation, per rule;
- the text builder round trip through `ParserService` for every fixture;
- `OpenRouterClient` against a stub `HttpMessageHandler`: request shape, headers, the `models` array, json_object fallback, error mapping, the timeout;
- the endpoint with the fake: the feature flag on and off, 401, the read-only token refusal, the hourly and daily limits, the size and type limits, each error code, the one retry;
- receipt drafts: owner gets it, another user gets 404, expired gets 404;
- the Profile setting round trip.
All existing tests still pass.

## Runtime check
Run the stack with `docker compose` (as earlier tasks did) with the fake provider: post a fixture image to `POST Bills/receipt` with a JWT, get the draft back by id, see `receiptParse` in `GET Auth/config`, hit the limit, and confirm that a read-only token gets 403. Bring it down and clean up afterwards (common.md).

## Docs
- `DebtBot.Doc/web-ui/api.md`: the new endpoints, error codes, limits and the config flag.
- The top-level `README.md`: a short "Receipts (optional)" settings section, saying the key is a secret set as an environment variable.
- `DebtBot.Doc/receipts/reports/task-01.md`, including what T2 and T3 need to know (the service and limiter entry points, the draft id and URL, the fake provider, the warning codes).

No UI, no screenshots.

When you're done, follow "Finishing" and "Waking the orchestrator" in common.md.
