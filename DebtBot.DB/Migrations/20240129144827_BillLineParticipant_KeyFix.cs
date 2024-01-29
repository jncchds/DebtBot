using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class BillLineParticipant_KeyFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BillLineParticipants",
                table: "BillLineParticipants");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BillLineParticipants",
                table: "BillLineParticipants",
                columns: new[] { "BillLineId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BillLineParticipants",
                table: "BillLineParticipants");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BillLineParticipants",
                table: "BillLineParticipants",
                column: "BillLineId");
        }
    }
}
