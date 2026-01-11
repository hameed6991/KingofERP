using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class addpetticashupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PettyCashVouchers_PettyCashClaims_PettyCashClaimId",
                table: "PettyCashVouchers");

            migrationBuilder.DropIndex(
                name: "IX_PettyCashVouchers_PettyCashClaimId",
                table: "PettyCashVouchers");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PettyCashVouchers");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "PettyCashVouchers");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "PettyCashVouchers",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "PettyCashVouchers",
                newName: "Method");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNo",
                table: "PettyCashVouchers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaidTo",
                table: "PettyCashVouchers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "PettyCashVouchers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PettyCashClaims",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "VoucherId",
                table: "PettyCashClaims",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherNo",
                table: "PettyCashClaims",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "PettyCashClaimLines",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PettyCashClaims");

            migrationBuilder.DropColumn(
                name: "VoucherId",
                table: "PettyCashClaims");

            migrationBuilder.DropColumn(
                name: "VoucherNo",
                table: "PettyCashClaims");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PettyCashClaimLines");

            migrationBuilder.RenameColumn(
                name: "Method",
                table: "PettyCashVouchers",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "PettyCashVouchers",
                newName: "TotalAmount");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNo",
                table: "PettyCashVouchers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaidTo",
                table: "PettyCashVouchers",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "PettyCashVouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "PettyCashVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostedAt",
                table: "PettyCashVouchers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashVouchers_PettyCashClaimId",
                table: "PettyCashVouchers",
                column: "PettyCashClaimId");

            migrationBuilder.AddForeignKey(
                name: "FK_PettyCashVouchers_PettyCashClaims_PettyCashClaimId",
                table: "PettyCashVouchers",
                column: "PettyCashClaimId",
                principalTable: "PettyCashClaims",
                principalColumn: "PettyCashClaimId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
