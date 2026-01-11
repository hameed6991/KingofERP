using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyShortNamenew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Employees");

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Companies",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Companies");

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
