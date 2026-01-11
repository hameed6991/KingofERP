using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class addpetticash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PettyCashClaims",
                columns: table => new
                {
                    PettyCashClaimId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ClaimNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ClaimDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PettyCashClaims", x => x.PettyCashClaimId);
                });

            migrationBuilder.CreateTable(
                name: "PettyCashClaimLines",
                columns: table => new
                {
                    PettyCashClaimLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PettyCashClaimId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpenseAccountNo = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceiptPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PettyCashClaimLines", x => x.PettyCashClaimLineId);
                    table.ForeignKey(
                        name: "FK_PettyCashClaimLines_PettyCashClaims_PettyCashClaimId",
                        column: x => x.PettyCashClaimId,
                        principalTable: "PettyCashClaims",
                        principalColumn: "PettyCashClaimId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PettyCashVouchers",
                columns: table => new
                {
                    PettyCashVoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    PettyCashClaimId = table.Column<int>(type: "int", nullable: false),
                    VoucherNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VoucherDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    CashAccountNo = table.Column<int>(type: "int", nullable: false),
                    PaidTo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PettyCashVouchers", x => x.PettyCashVoucherId);
                    table.ForeignKey(
                        name: "FK_PettyCashVouchers_PettyCashClaims_PettyCashClaimId",
                        column: x => x.PettyCashClaimId,
                        principalTable: "PettyCashClaims",
                        principalColumn: "PettyCashClaimId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashClaimLines_PettyCashClaimId",
                table: "PettyCashClaimLines",
                column: "PettyCashClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashClaims_CompanyId_ClaimNo",
                table: "PettyCashClaims",
                columns: new[] { "CompanyId", "ClaimNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashVouchers_CompanyId_VoucherNo",
                table: "PettyCashVouchers",
                columns: new[] { "CompanyId", "VoucherNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashVouchers_PettyCashClaimId",
                table: "PettyCashVouchers",
                column: "PettyCashClaimId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PettyCashClaimLines");

            migrationBuilder.DropTable(
                name: "PettyCashVouchers");

            migrationBuilder.DropTable(
                name: "PettyCashClaims");
        }
    }
}
