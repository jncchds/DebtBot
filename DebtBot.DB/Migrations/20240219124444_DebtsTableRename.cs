using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class DebtsTableRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Debts_Users_CreditorUserId",
                table: "Debts");

            migrationBuilder.DropForeignKey(
                name: "FK_Debts_Users_DebtorUserId",
                table: "Debts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Debts",
                table: "Debts");

            migrationBuilder.RenameTable(
                name: "Debts",
                newName: "DebtsTable");

            migrationBuilder.RenameIndex(
                name: "IX_Debts_DebtorUserId",
                table: "DebtsTable",
                newName: "IX_DebtsTable_DebtorUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DebtsTable",
                table: "DebtsTable",
                columns: new[] { "CreditorUserId", "DebtorUserId", "CurrencyCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_DebtsTable_Users_CreditorUserId",
                table: "DebtsTable",
                column: "CreditorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DebtsTable_Users_DebtorUserId",
                table: "DebtsTable",
                column: "DebtorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DebtsTable_Users_CreditorUserId",
                table: "DebtsTable");

            migrationBuilder.DropForeignKey(
                name: "FK_DebtsTable_Users_DebtorUserId",
                table: "DebtsTable");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DebtsTable",
                table: "DebtsTable");

            migrationBuilder.RenameTable(
                name: "DebtsTable",
                newName: "Debts");

            migrationBuilder.RenameIndex(
                name: "IX_DebtsTable_DebtorUserId",
                table: "Debts",
                newName: "IX_Debts_DebtorUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Debts",
                table: "Debts",
                columns: new[] { "CreditorUserId", "DebtorUserId", "CurrencyCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_Users_CreditorUserId",
                table: "Debts",
                column: "CreditorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_Users_DebtorUserId",
                table: "Debts",
                column: "DebtorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
