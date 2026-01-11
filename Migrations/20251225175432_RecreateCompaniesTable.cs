using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class RecreateCompaniesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
    name: "Companies",
    columns: table => new
    {
        CompanyId = table.Column<int>(type: "int", nullable: false), // ✅ no identity
        LegalName = table.Column<string>(type: "nvarchar(max)", nullable: true),
        TRN = table.Column<string>(type: "nvarchar(max)", nullable: true),
        Emirate = table.Column<string>(type: "nvarchar(max)", nullable: true),
        City = table.Column<string>(type: "nvarchar(max)", nullable: true),
        InvoicePrefix = table.Column<string>(type: "nvarchar(max)", nullable: true),
        NextInvoiceNumber = table.Column<int>(type: "int", nullable: false),
        DefaultVatRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_Companies", x => x.CompanyId);
    });


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
