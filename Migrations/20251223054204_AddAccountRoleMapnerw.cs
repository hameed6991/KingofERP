using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountRoleMapnerw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "AccountRoleMaps");

            migrationBuilder.RenameColumn(
                name: "AccountRoleMapId",
                table: "AccountRoleMaps",
                newName: "Id");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AccountRoleMaps",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RoleKey",
                table: "AccountRoleMaps",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AccountRoleMaps",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AccountRoleMaps");

            migrationBuilder.DropColumn(
                name: "RoleKey",
                table: "AccountRoleMaps");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AccountRoleMaps");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AccountRoleMaps",
                newName: "AccountRoleMapId");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AccountRoleMaps",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }
    }
}
