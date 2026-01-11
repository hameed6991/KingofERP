using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorInvoiceNoAndLineNetnew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QtyIn",
                table: "StockLedgers");

            migrationBuilder.DropColumn(
                name: "RefId",
                table: "StockLedgers");

            migrationBuilder.RenameColumn(
                name: "RefType",
                table: "StockLedgers",
                newName: "TxnType");

            migrationBuilder.RenameColumn(
                name: "QtyOut",
                table: "StockLedgers",
                newName: "QtyChange");

            migrationBuilder.AddColumn<string>(
                name: "ItemName",
                table: "StockLedgers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "StockLedgers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefNo",
                table: "StockLedgers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemName",
                table: "StockLedgers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "StockLedgers");

            migrationBuilder.DropColumn(
                name: "RefNo",
                table: "StockLedgers");

            migrationBuilder.RenameColumn(
                name: "TxnType",
                table: "StockLedgers",
                newName: "RefType");

            migrationBuilder.RenameColumn(
                name: "QtyChange",
                table: "StockLedgers",
                newName: "QtyOut");

            migrationBuilder.AddColumn<decimal>(
                name: "QtyIn",
                table: "StockLedgers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RefId",
                table: "StockLedgers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
