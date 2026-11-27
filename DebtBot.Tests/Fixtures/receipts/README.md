# Receipt fixtures

Model answers in the `receipt` schema (`DebtBot/Receipts/ReceiptSchema.cs`), written by hand. They're embedded in the
app for `FakeLlmClient` (`Llm:Provider=Fake`, Development only), which picks the one whose name is in the uploaded
file's name. `DebtBot.Tests/ReceiptTests.cs` uses them too.

| Fixture | What it covers |
|---|---|
| `cafe-eur` | the default: a cafe in EUR, an ISO code, quantities, tax included |
| `supermarket-discount` | only the `€` sign, a date without a time, a weighed item, a negative discount line |
| `decimal-comma` | `1.234,50` style amounts, `4 574,50` with a space, `руб`/`₽`, a service charge outside the total |
| `dollar-ambiguous` | a plain `$` (the last bill's currency decides), tax on top of the items |
| `tips-outside` | tips printed outside the total |
| `not-a-receipt` | `isReceipt: false` |
| `broken-json` | an answer cut off mid-way (invalid JSON) |
