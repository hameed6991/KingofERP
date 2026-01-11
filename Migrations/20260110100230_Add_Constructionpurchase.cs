using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class Add_Constructionpurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstructionPurchaseBills",
                columns: table => new
                {
                    ConstructionPurchaseBillId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BillNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BillNoSeq = table.Column<int>(type: "int", nullable: false),
                    BillDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VendorTRN = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    VendorInvoiceNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiveMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionPurchaseBills", x => x.ConstructionPurchaseBillId);
                });

            migrationBuilder.CreateTable(
                name: "DocSequences",
                columns: table => new
                {
                    DocSequenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DocType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false),
                    Pad = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocSequences", x => x.DocSequenceId);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionPurchaseBillLines",
                columns: table => new
                {
                    ConstructionPurchaseBillLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ConstructionPurchaseBillId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineNet = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineVat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionPurchaseBillLines", x => x.ConstructionPurchaseBillLineId);
                    table.ForeignKey(
                        name: "FK_ConstructionPurchaseBillLines_ConstructionPurchaseBills_ConstructionPurchaseBillId",
                        column: x => x.ConstructionPurchaseBillId,
                        principalTable: "ConstructionPurchaseBills",
                        principalColumn: "ConstructionPurchaseBillId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionPurchaseBillLines_ConstructionPurchaseBillId",
                table: "ConstructionPurchaseBillLines",
                column: "ConstructionPurchaseBillId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionPurchaseBills_CompanyId_BillNo",
                table: "ConstructionPurchaseBills",
                columns: new[] { "CompanyId", "BillNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocSequences_CompanyId_DocType",
                table: "DocSequences",
                columns: new[] { "CompanyId", "DocType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstructionPurchaseBillLines");

            migrationBuilder.DropTable(
                name: "DocSequences");

            migrationBuilder.DropTable(
                name: "ConstructionPurchaseBills");
        }
    }
}
