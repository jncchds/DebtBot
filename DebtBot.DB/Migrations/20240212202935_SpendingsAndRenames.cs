using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DebtBot.DB.Migrations
{
    /// <inheritdoc />
    public partial class SpendingsAndRenames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Spandings_Bills_BillId",
                table: "Spandings");

            migrationBuilder.DropForeignKey(
                name: "FK_Spandings_Users_UserId",
                table: "Spandings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Spandings",
                table: "Spandings");

            migrationBuilder.RenameTable(
                name: "Spandings",
                newName: "Spendings");

            migrationBuilder.RenameColumn(
                name: "Total",
                table: "Bills",
                newName: "TotalWithTips");

            migrationBuilder.RenameIndex(
                name: "IX_Spandings_UserId",
                table: "Spendings",
                newName: "IX_Spendings_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Spendings",
                table: "Spendings",
                columns: new[] { "BillId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Spendings_Bills_BillId",
                table: "Spendings",
                column: "BillId",
                principalTable: "Bills",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Spendings_Bills_BillId",
                table: "Spendings");

            migrationBuilder.DropForeignKey(
                name: "FK_Spendings_Users_UserId",
                table: "Spendings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Spendings",
                table: "Spendings");

            migrationBuilder.RenameTable(
                name: "Spendings",
                newName: "Spandings");

            migrationBuilder.RenameColumn(
                name: "TotalWithTips",
                table: "Bills",
                newName: "Total");

            migrationBuilder.RenameIndex(
                name: "IX_Spendings_UserId",
                table: "Spandings",
                newName: "IX_Spandings_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Spandings",
                table: "Spandings",
                columns: new[] { "BillId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Spandings_Bills_BillId",
                table: "Spandings",
                column: "BillId",
                principalTable: "Bills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Spandings_Users_UserId",
                table: "Spandings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
