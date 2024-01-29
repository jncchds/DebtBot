using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class LedgerRecordStatusAndKeyFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ledgers_Bills_BillId",
                table: "Ledgers");

            migrationBuilder.DropForeignKey(
                name: "FK_Ledgers_Users_CreditorUserId",
                table: "Ledgers");

            migrationBuilder.DropForeignKey(
                name: "FK_Ledgers_Users_DebtorUserId",
                table: "Ledgers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ledgers",
                table: "Ledgers");

            migrationBuilder.RenameTable(
                name: "Ledgers",
                newName: "LedgerRecords");

            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "LedgerRecords",
                newName: "CurrencyCode");

            migrationBuilder.RenameIndex(
                name: "IX_Ledgers_DebtorUserId",
                table: "LedgerRecords",
                newName: "IX_LedgerRecords_DebtorUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Ledgers_BillId",
                table: "LedgerRecords",
                newName: "IX_LedgerRecords_BillId");

            migrationBuilder.AddColumn<byte>(
                name: "Status",
                table: "LedgerRecords",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LedgerRecords",
                table: "LedgerRecords",
                columns: new[] { "CreditorUserId", "DebtorUserId", "BillId" });

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerRecords_Bills_BillId",
                table: "LedgerRecords",
                column: "BillId",
                principalTable: "Bills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerRecords_Users_CreditorUserId",
                table: "LedgerRecords",
                column: "CreditorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerRecords_Users_DebtorUserId",
                table: "LedgerRecords",
                column: "DebtorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerRecords_Bills_BillId",
                table: "LedgerRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LedgerRecords_Users_CreditorUserId",
                table: "LedgerRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LedgerRecords_Users_DebtorUserId",
                table: "LedgerRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LedgerRecords",
                table: "LedgerRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LedgerRecords");

            migrationBuilder.RenameTable(
                name: "LedgerRecords",
                newName: "Ledgers");

            migrationBuilder.RenameColumn(
                name: "CurrencyCode",
                table: "Ledgers",
                newName: "Currency");

            migrationBuilder.RenameIndex(
                name: "IX_LedgerRecords_DebtorUserId",
                table: "Ledgers",
                newName: "IX_Ledgers_DebtorUserId");

            migrationBuilder.RenameIndex(
                name: "IX_LedgerRecords_BillId",
                table: "Ledgers",
                newName: "IX_Ledgers_BillId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ledgers",
                table: "Ledgers",
                columns: new[] { "CreditorUserId", "DebtorUserId", "Currency" });

            migrationBuilder.AddForeignKey(
                name: "FK_Ledgers_Bills_BillId",
                table: "Ledgers",
                column: "BillId",
                principalTable: "Bills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ledgers_Users_CreditorUserId",
                table: "Ledgers",
                column: "CreditorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ledgers_Users_DebtorUserId",
                table: "Ledgers",
                column: "DebtorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
