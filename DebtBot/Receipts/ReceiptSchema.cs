using Json.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DebtBot.Receipts;

/// <summary>The model's answer: the strict schema sent to the model, and the lenient one we validate with.</summary>
public static class ReceiptSchema
{
    public const string Name = "receipt";

    /// <summary>
    /// Strict mode: every property required, nothing extra, nulls where a value may be missing. Amounts and
    /// quantities are strings so that "1.234,50" or a rounding never passes through a float.
    /// </summary>
    public const string StrictJson = """
    {
      "type": "object",
      "properties": {
        "isReceipt": { "type": "boolean" },
        "merchant": { "type": ["string", "null"] },
        "date": { "type": ["string", "null"], "description": "ISO 8601 as printed: 2026-11-20T19:42, or 2026-11-20 when no time is printed" },
        "currency": { "type": ["string", "null"], "description": "ISO 4217 code if printed, else null" },
        "currencySymbol": { "type": ["string", "null"], "description": "the currency sign as printed, e.g. €, $, ₽" },
        "items": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "name": { "type": "string" },
              "quantity": { "type": ["string", "null"] },
              "amount": { "type": "string", "description": "the line total (quantity × price, discounts applied), as printed" }
            },
            "required": ["name", "quantity", "amount"],
            "additionalProperties": false
          }
        },
        "tips": { "type": ["string", "null"] },
        "tipsIncluded": { "type": ["boolean", "null"], "description": "true if the tips are part of total" },
        "serviceCharge": { "type": ["string", "null"] },
        "serviceChargeIncluded": { "type": ["boolean", "null"], "description": "true if the service charge is part of total" },
        "tax": { "type": ["string", "null"] },
        "taxIncluded": { "type": ["boolean", "null"], "description": "true if the item amounts already include the tax" },
        "total": { "type": ["string", "null"], "description": "the printed total to pay" }
      },
      "required": ["isReceipt", "merchant", "date", "currency", "currencySymbol", "items", "tips", "tipsIncluded",
        "serviceCharge", "serviceChargeIncluded", "tax", "taxIncluded", "total"],
      "additionalProperties": false
    }
    """;

    /// <summary>
    /// What we accept: only <c>isReceipt</c> and <c>items</c> are required, amounts may also be numbers (some
    /// json_object models send them), extra properties are ignored.
    /// </summary>
    private const string LenientJson = """
    {
      "type": "object",
      "properties": {
        "isReceipt": { "type": "boolean" },
        "merchant": { "type": ["string", "null"] },
        "date": { "type": ["string", "null"] },
        "currency": { "type": ["string", "null"] },
        "currencySymbol": { "type": ["string", "null"] },
        "items": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "name": { "type": ["string", "null"] },
              "quantity": { "type": ["string", "number", "null"] },
              "amount": { "type": ["string", "number", "null"] }
            }
          }
        },
        "tips": { "type": ["string", "number", "null"] },
        "tipsIncluded": { "type": ["boolean", "null"] },
        "serviceCharge": { "type": ["string", "number", "null"] },
        "serviceChargeIncluded": { "type": ["boolean", "null"] },
        "tax": { "type": ["string", "number", "null"] },
        "taxIncluded": { "type": ["boolean", "null"] },
        "total": { "type": ["string", "number", "null"] }
      },
      "required": ["isReceipt", "items"]
    }
    """;

    public static JsonNode Strict() => JsonNode.Parse(StrictJson)!;

    private static readonly Lazy<JsonSchema> Lenient = new(() =>
        JsonSchema.FromText(LenientJson, baseUri: new Uri("https://debtbot.local/schemas/receipt-lenient.json")));

    /// <summary>True when the answer fits the lenient schema.</summary>
    public static bool IsValid(JsonElement answer) =>
        Lenient.Value.Evaluate(answer, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid;

    /// <summary>True for <c>{ "isReceipt": false }</c>, whatever else the answer holds.</summary>
    public static bool IsNotAReceipt(JsonElement answer) =>
        answer.ValueKind == JsonValueKind.Object
        && answer.TryGetProperty("isReceipt", out var isReceipt)
        && isReceipt.ValueKind == JsonValueKind.False;

    public const string UserText = "Read this receipt.";

    /// <summary>The rules from the design (receipt-parse.md, "Prompt rules"), plus the schema for json_object models.</summary>
    public static readonly string SystemPrompt = $$"""
    You read photos of shop and restaurant receipts and answer with one JSON object, nothing else.
    Rules:
    - Read only what is printed. Never invent items, amounts or names. If something isn't printed, use null.
    - One entry in "items" per printed line item, in the printed order. "amount" is the line total
      (quantity × price) as printed. Apply a discount printed under an item to that item's amount; a discount that
      belongs to no single item is its own item with a negative amount.
    - Write amounts and quantities as strings, exactly as printed (keep the receipt's decimal separator).
    - Tips, service charge and tax go in their own fields, never in "items". Set "tipsIncluded" and
      "serviceChargeIncluded" to true when they are already part of the printed total, false when the total is
      printed without them. Set "taxIncluded" to true when the item amounts already include the tax.
    - "total" is the printed total to pay.
    - "currency" is the ISO 4217 code if one is printed; "currencySymbol" is the sign as printed (€, $, £, ₽).
    - "date" is ISO 8601: "2026-11-20T19:42" when a time is printed, "2026-11-20" when only a date is.
    - If the image is not a receipt, answer {"isReceipt": false} with the other fields null and "items" empty.
    The answer must match this JSON Schema:
    {{StrictJson}}
    """;
}
