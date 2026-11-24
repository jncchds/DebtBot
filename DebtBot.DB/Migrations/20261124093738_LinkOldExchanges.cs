using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations;

/// <summary>
/// Data only: links exchange bill pairs that the bot's /ExchangeDebt made before Bills.ExchangeBillId existed,
/// so they show in GET Bills/exchanges. A pair is linked only when it's certain:
/// - both bills are unlinked, have the same creator and status, and were made within a minute of each other;
/// - each has exactly one payment and one "Exchange" line with exactly one participant, and its description is
///   "&lt;text&gt;\nExchange &lt;rate&gt; &lt;A&gt;/&lt;B&gt;" with the same text on both;
/// - they mirror each other: one bill's payer is the other's line participant (two different people), the
///   currencies are swapped, and each bill's payment equals the other's total;
/// - neither bill matches any other bill the same way.
/// Running it again changes nothing (only unlinked bills are touched). Down leaves the links: they're correct data.
/// </summary>
public partial class _20261124093738_LinkOldExchanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            WITH ex AS (
                SELECT b."Id", b."CreatorId", b."Status", b."Date", b."CurrencyCode", b."PaymentCurrencyCode",
                       b."TotalWithTips",
                       regexp_replace(b."Description", E'\nExchange [^\n]*$', '') AS "BaseDescription",
                       p."UserId" AS "PayerId", p."Amount" AS "Paid", lp."UserId" AS "ReceiverId"
                FROM "Bills" b
                JOIN "BillPayments" p ON p."BillId" = b."Id"
                JOIN "BillLines" l ON l."BillId" = b."Id"
                JOIN "BillLineParticipants" lp ON lp."BillLineId" = l."Id"
                WHERE b."ExchangeBillId" IS NULL
                  AND b."Description" ~ E'\nExchange [^\n]*$'
                  AND l."ItemDescription" = 'Exchange'
                  AND (SELECT count(*) FROM "BillPayments" x WHERE x."BillId" = b."Id") = 1
                  AND (SELECT count(*) FROM "BillLines" x WHERE x."BillId" = b."Id") = 1
                  AND (SELECT count(*) FROM "BillLineParticipants" x
                       JOIN "BillLines" y ON y."Id" = x."BillLineId" WHERE y."BillId" = b."Id") = 1
            ),
            pairs AS (
                SELECT a."Id" AS "BillId", c."Id" AS "PairId"
                FROM ex a
                JOIN ex c ON c."Id" <> a."Id"
                    AND c."CreatorId" = a."CreatorId"
                    AND c."Status" = a."Status"
                    AND c."BaseDescription" = a."BaseDescription"
                    AND c."PayerId" = a."ReceiverId"
                    AND c."ReceiverId" = a."PayerId"
                    AND a."PayerId" <> a."ReceiverId"
                    AND c."CurrencyCode" = a."PaymentCurrencyCode"
                    AND c."PaymentCurrencyCode" = a."CurrencyCode"
                    AND c."Paid" = a."TotalWithTips"
                    AND a."Paid" = c."TotalWithTips"
                    AND abs(extract(epoch FROM (c."Date" - a."Date"))) < 60
            ),
            certain AS (
                SELECT p."BillId", p."PairId"
                FROM pairs p
                WHERE (SELECT count(*) FROM pairs q WHERE q."BillId" = p."BillId") = 1
                  AND (SELECT count(*) FROM pairs q WHERE q."BillId" = p."PairId") = 1
            )
            UPDATE "Bills" b
            SET "ExchangeBillId" = certain."PairId"
            FROM certain
            WHERE b."Id" = certain."BillId" AND b."ExchangeBillId" IS NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
