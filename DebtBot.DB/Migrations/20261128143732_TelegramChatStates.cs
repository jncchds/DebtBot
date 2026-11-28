using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations;

/// <inheritdoc />
public partial class _20261128143732_TelegramChatStates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TelegramChatStates",
            columns: table => new
            {
                ChatId = table.Column<long>(type: "bigint", nullable: false),
                ReceiptNoteShownAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TelegramChatStates", x => x.ChatId);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TelegramChatStates");
    }
}
