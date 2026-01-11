using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerNotesAndFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsImportant",
                table: "CustomerNotes",
                newName: "IsPinned");

            migrationBuilder.AlterColumn<string>(
                name: "NoteText",
                table: "CustomerNotes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "CustomerNotes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPinned",
                table: "CustomerNotes",
                newName: "IsImportant");

            migrationBuilder.AlterColumn<string>(
                name: "NoteText",
                table: "CustomerNotes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "CustomerNotes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);
        }
    }
}
