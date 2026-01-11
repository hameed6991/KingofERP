using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class Cheques_Modulenew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ChequeNo",
                table: "ChequeTransactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeTransactions_CompanyId_ChequeBookId_ChequeNo",
                table: "ChequeTransactions",
                columns: new[] { "CompanyId", "ChequeBookId", "ChequeNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ChequeTransactions_CompanyId_Direction_Status_ChequeDate",
                table: "ChequeTransactions",
                columns: new[] { "CompanyId", "Direction", "Status", "ChequeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ChequeBooks_CompanyId_BankAccountNo_StartNo_EndNo",
                table: "ChequeBooks",
                columns: new[] { "CompanyId", "BankAccountNo", "StartNo", "EndNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChequeTransactions_CompanyId_ChequeBookId_ChequeNo",
                table: "ChequeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ChequeTransactions_CompanyId_Direction_Status_ChequeDate",
                table: "ChequeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ChequeBooks_CompanyId_BankAccountNo_StartNo_EndNo",
                table: "ChequeBooks");

            migrationBuilder.AlterColumn<string>(
                name: "ChequeNo",
                table: "ChequeTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
