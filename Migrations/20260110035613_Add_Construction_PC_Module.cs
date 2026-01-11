using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeEInvoice.Migrations
{
    /// <inheritdoc />
    public partial class Add_Construction_PC_Module : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstructionAccountsMaps",
                columns: table => new
                {
                    ConstructionAccountsMapId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    SubcontractCostAccountNo = table.Column<int>(type: "int", nullable: false),
                    AccountsPayableAccountNo = table.Column<int>(type: "int", nullable: false),
                    RetentionPayableAccountNo = table.Column<int>(type: "int", nullable: false),
                    VatInputAccountNo = table.Column<int>(type: "int", nullable: false),
                    BackchargeRecoveryAccountNo = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionAccountsMaps", x => x.ConstructionAccountsMapId);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionSubcontractors",
                columns: table => new
                {
                    ConstructionSubcontractorId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TRN = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DefaultRetentionRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionSubcontractors", x => x.ConstructionSubcontractorId);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractorPCs",
                columns: table => new
                {
                    SubcontractorPCId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ConstructionProjectId = table.Column<int>(type: "int", nullable: false),
                    ConstructionSubcontractorId = table.Column<int>(type: "int", nullable: false),
                    PCNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PeriodMonth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetentionRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousCumulative = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BackchargeThisMonth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WorkDoneToDate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThisMonthGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetentionThisMonth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayableExVat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPayable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
                    PostedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractorPCs", x => x.SubcontractorPCId);
                    table.ForeignKey(
                        name: "FK_SubcontractorPCs_ConstructionProjects_ConstructionProjectId",
                        column: x => x.ConstructionProjectId,
                        principalTable: "ConstructionProjects",
                        principalColumn: "ConstructionProjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubcontractorPCs_ConstructionSubcontractors_ConstructionSubcontractorId",
                        column: x => x.ConstructionSubcontractorId,
                        principalTable: "ConstructionSubcontractors",
                        principalColumn: "ConstructionSubcontractorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractorPCLines",
                columns: table => new
                {
                    SubcontractorPCLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    SubcontractorPCId = table.Column<int>(type: "int", nullable: false),
                    WorkSection = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractorPCLines", x => x.SubcontractorPCLineId);
                    table.ForeignKey(
                        name: "FK_SubcontractorPCLines_SubcontractorPCs_SubcontractorPCId",
                        column: x => x.SubcontractorPCId,
                        principalTable: "SubcontractorPCs",
                        principalColumn: "SubcontractorPCId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionAccountsMaps_CompanyId_IsActive",
                table: "ConstructionAccountsMaps",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionSubcontractors_CompanyId_IsActive",
                table: "ConstructionSubcontractors",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionSubcontractors_CompanyId_Name",
                table: "ConstructionSubcontractors",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorPCLines_CompanyId_SubcontractorPCId",
                table: "SubcontractorPCLines",
                columns: new[] { "CompanyId", "SubcontractorPCId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorPCLines_SubcontractorPCId",
                table: "SubcontractorPCLines",
                column: "SubcontractorPCId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorPCs_CompanyId_ConstructionProjectId_ConstructionSubcontractorId_PeriodMonth",
                table: "SubcontractorPCs",
                columns: new[] { "CompanyId", "ConstructionProjectId", "ConstructionSubcontractorId", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorPCs_CompanyId_PCNo",
                table: "SubcontractorPCs",
                columns: new[] { "CompanyId", "PCNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorPCs_ConstructionProjectId",
                table: "SubcontractorPCs",
                column: "ConstructionProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorPCs_ConstructionSubcontractorId",
                table: "SubcontractorPCs",
                column: "ConstructionSubcontractorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstructionAccountsMaps");

            migrationBuilder.DropTable(
                name: "SubcontractorPCLines");

            migrationBuilder.DropTable(
                name: "SubcontractorPCs");

            migrationBuilder.DropTable(
                name: "ConstructionSubcontractors");
        }
    }
}
