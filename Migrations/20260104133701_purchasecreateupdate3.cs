using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class purchasecreateupdate3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add column as NULL first (NO DEFAULT)
            migrationBuilder.AddColumn<int>(
                name: "PurchaseNoSeq",
                table: "PurchaseInvoices",
                type: "int",
                nullable: true);

            // 2) Backfill existing rows per company using ROW_NUMBER()
            migrationBuilder.Sql(@"
;WITH cte AS (
    SELECT PurchaseInvoiceId, CompanyId,
           rn = ROW_NUMBER() OVER (PARTITION BY CompanyId ORDER BY PurchaseInvoiceId)
    FROM dbo.PurchaseInvoices
)
UPDATE p
SET PurchaseNoSeq = cte.rn
FROM dbo.PurchaseInvoices p
JOIN cte ON p.PurchaseInvoiceId = cte.PurchaseInvoiceId;
");

            // 3) Now make it NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "PurchaseNoSeq",
                table: "PurchaseInvoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 4) Create unique index
            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_CompanyId_PurchaseNoSeq",
                table: "PurchaseInvoices",
                columns: new[] { "CompanyId", "PurchaseNoSeq" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_CompanyId_PurchaseNoSeq",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PurchaseNoSeq",
                table: "PurchaseInvoices");
        }


        /// <inheritdoc />
        //protected override void Down(MigrationBuilder migrationBuilder)
        //{
        //    migrationBuilder.DropIndex(
        //        name: "IX_PurchaseInvoices_CompanyId_PurchaseNoSeq",
        //        table: "PurchaseInvoices");

        //    migrationBuilder.DropColumn(
        //        name: "PurchaseNoSeq",
        //        table: "PurchaseInvoices");
        //}
    }
}
