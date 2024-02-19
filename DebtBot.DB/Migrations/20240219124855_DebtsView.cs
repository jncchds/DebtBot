using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class DebtsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // materialized view
            migrationBuilder.Sql(@"
create materialized view if not exists Debts as
select
    ""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"", sum(""Amount"") as ""Amount""
from
    (
        select ""CreditorUserId"", ""DebtorUserId"", ""Amount"", ""CurrencyCode""
        from
            ""LedgerRecords""

        union

        select ""DebtorUserId"" as ""CreditorUserId"", ""CreditorUserId"" as ""DebtorUserId"", ""Amount""*(-1) as ""Amount"", ""CurrencyCode""
        from
            ""LedgerRecords""
    ) as ""ledgers""
group by
    ""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"";
");

            // index
            migrationBuilder.Sql(@"
create unique index Debts_view_index
on ""debts""
(""CreditorUserId"", ""DebtorUserId"", ""CurrencyCode"")
");
            
            // refresh view function
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION Refresh_debts_view()
    RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY Debts;
    RETURN NULL;
END;
$$;
");

            // trigger to refresh
            migrationBuilder.Sql(@"
create or replace trigger Ledger_record_updated
after insert or update on ""LedgerRecords""
execute function Refresh_debts_view()
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
	        migrationBuilder.Sql(@"
drop trigger Ledger_record_updated on ""LedgerRecords""
");

	        migrationBuilder.Sql(@"
drop function Refresh_debts_view_func
");

	        migrationBuilder.Sql(@"
drop materialized view Debts cascade
");
        }
    }
}
