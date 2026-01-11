using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class bankaccountup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Confidence",
                table: "BankStatementLines",
                type: "decimal(5,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_CompanyId_BankAccountId_TxnDate",
                table: "BankStatementLines",
                columns: new[] { "CompanyId", "BankAccountId", "TxnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId_AccountNo",
                table: "BankAccounts",
                columns: new[] { "CompanyId", "AccountNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankStatementLines_CompanyId_BankAccountId_TxnDate",
                table: "BankStatementLines");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CompanyId_AccountNo",
                table: "BankAccounts");

            migrationBuilder.AlterColumn<decimal>(
                name: "Confidence",
                table: "BankStatementLines",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldNullable: true);
        }
    }
}
