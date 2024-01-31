using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class CompoundKeyOnDebt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Debts",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_Debts_CreditorUserId",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Debts");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Debts",
                table: "Debts",
                columns: new[] { "CreditorUserId", "DebtorUserId", "CurrencyCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Debts",
                table: "Debts");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "Debts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Debts",
                table: "Debts",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Debts_CreditorUserId",
                table: "Debts",
                column: "CreditorUserId");
        }
    }
}
