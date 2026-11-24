using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class ExchangePair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeBillId",
                table: "Bills",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeBillId",
                table: "Bills");
        }
    }
}
