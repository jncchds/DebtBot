using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class BillExtraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "Bills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PaymentCurrencyCode",
                table: "Bills",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Bills",
                type: "numeric(10,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "BillLines",
                type: "numeric(10,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CreatorId",
                table: "Bills",
                column: "CreatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Users_CreatorId",
                table: "Bills",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Users_CreatorId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_CreatorId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PaymentCurrencyCode",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Bills");

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "BillLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,4)");
        }
    }
}
