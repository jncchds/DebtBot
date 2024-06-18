using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class RestrictUserDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerRecords_Users_CreditorUserId",
                table: "LedgerRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LedgerRecords_Users_DebtorUserId",
                table: "LedgerRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Spendings_Users_UserId",
                table: "Spendings");

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerRecords_Users_CreditorUserId",
                table: "LedgerRecords",
                column: "CreditorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerRecords_Users_DebtorUserId",
                table: "LedgerRecords",
                column: "DebtorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Spendings_Users_UserId",
                table: "Spendings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerRecords_Users_CreditorUserId",
                table: "LedgerRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LedgerRecords_Users_DebtorUserId",
                table: "LedgerRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Spendings_Users_UserId",
                table: "Spendings");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Spendings_Users_UserId",
                table: "Spendings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
