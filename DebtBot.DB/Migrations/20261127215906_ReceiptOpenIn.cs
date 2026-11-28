using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations;

/// <inheritdoc />
public partial class _20261127215906_ReceiptOpenIn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ReceiptOpenIn",
            table: "Users",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReceiptOpenIn",
            table: "Users");
    }
}
