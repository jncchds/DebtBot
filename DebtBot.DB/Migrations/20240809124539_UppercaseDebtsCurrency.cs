using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class UppercaseDebtsCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // materialized view
            migrationBuilder.Sql(@"
-- materialized view
DROP MATERIALIZED VIEW IF EXISTS Debts;
CREATE MATERIALIZED VIEW Debts AS
SELECT
    ""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"", SUM(""Amount"") AS ""Amount""
FROM
    (
        SELECT ""CreditorUserId"", ""DebtorUserId"", ""Amount"", UPPER(""CurrencyCode"") as ""CurrencyCode""
        FROM ""LedgerRecords""
        WHERE ""IsCanceled"" = false

        UNION ALL

        SELECT ""DebtorUserId"" AS ""CreditorUserId"", ""CreditorUserId"" AS ""DebtorUserId"", ""Amount"" * (-1) AS ""Amount"", UPPER(""CurrencyCode"") as ""CurrencyCode""
        FROM ""LedgerRecords""
        WHERE ""IsCanceled"" = false
    ) AS ""ledgers""
GROUP BY
    ""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"";

CREATE UNIQUE INDEX ON Debts (""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // materialized view
            migrationBuilder.Sql(@"
-- materialized view
DROP MATERIALIZED VIEW IF EXISTS Debts;
CREATE MATERIALIZED VIEW Debts AS
SELECT
    ""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"", SUM(""Amount"") AS ""Amount""
FROM
    (
        SELECT ""CreditorUserId"", ""DebtorUserId"", ""Amount"", ""CurrencyCode""
        FROM ""LedgerRecords""
        WHERE ""IsCanceled"" = false

        UNION ALL

        SELECT ""DebtorUserId"" AS ""CreditorUserId"", ""CreditorUserId"" AS ""DebtorUserId"", ""Amount"" * (-1) AS ""Amount"", ""CurrencyCode""
        FROM ""LedgerRecords""
        WHERE ""IsCanceled"" = false
    ) AS ""ledgers""
GROUP BY
    ""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"";

CREATE UNIQUE INDEX ON Debts (""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"");
");
        }
    }
}
