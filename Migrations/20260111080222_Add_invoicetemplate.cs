using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class Add_invoicetemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SelectedTemplateId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateSnapshotJson",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceTemplates",
                columns: table => new
                {
                    InvoiceTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    IndustryTag = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BaseKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomCss = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceTemplates", x => x.InvoiceTemplateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_CompanyId_IsSystem_IsActive",
                table: "InvoiceTemplates",
                columns: new[] { "CompanyId", "IsSystem", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "SelectedTemplateId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TemplateSnapshotJson",
                table: "Invoices");
        }
    }
}
