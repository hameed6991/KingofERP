using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class AddRefNoNarrationToLedgerEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Narration",
                table: "LedgerEntries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RefNo",
                table: "LedgerEntries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Narration",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "RefNo",
                table: "LedgerEntries");
        }
    }
}
