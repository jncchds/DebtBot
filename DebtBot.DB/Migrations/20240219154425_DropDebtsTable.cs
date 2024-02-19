using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class DropDebtsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DebtsTable");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DebtsTable",
                columns: table => new
                {
                    CreditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DebtorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtsTable", x => new { x.CreditorUserId, x.DebtorUserId, x.CurrencyCode });
                    table.ForeignKey(
                        name: "FK_DebtsTable_Users_CreditorUserId",
                        column: x => x.CreditorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DebtsTable_Users_DebtorUserId",
                        column: x => x.DebtorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DebtsTable_DebtorUserId",
                table: "DebtsTable",
                column: "DebtorUserId");
        }
    }
}
