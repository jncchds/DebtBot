using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations;

/// <inheritdoc />
public partial class _20261124165718_ApiTokens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApiTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Scope = table.Column<byte>(type: "smallint", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApiTokens_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApiTokens_Hash",
            table: "ApiTokens",
            column: "Hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApiTokens_UserId",
            table: "ApiTokens",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApiTokens");
    }
}
