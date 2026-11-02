using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationRequests.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrganizationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Experts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    ActiveRequestCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Experts", x => x.Id);
                    table.CheckConstraint("CK_Experts_ValidCapacity", "[Capacity] > 0 AND [ActiveRequestCount] >= 0 AND [ActiveRequestCount] <= [Capacity]");
                });

            migrationBuilder.CreateTable(
                name: "ExpertSpecialties",
                columns: table => new
                {
                    ExpertId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertSpecialties", x => new { x.ExpertId, x.Subject });
                    table.CheckConstraint("CK_ExpertSpecialties_ValidSubject", "[Subject] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ExpertSpecialties_Experts_ExpertId",
                        column: x => x.ExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedExpertId = table.Column<int>(type: "int", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                    table.CheckConstraint("CK_ServiceRequests_ValidLifecycle", "([Status] = 1 AND [AssignedExpertId] IS NULL AND [AssignedAt] IS NULL AND [ClosedAt] IS NULL) OR ([Status] = 2 AND [AssignedExpertId] IS NOT NULL AND [AssignedAt] IS NOT NULL AND [ClosedAt] IS NULL) OR ([Status] = 3 AND [AssignedExpertId] IS NOT NULL AND [AssignedAt] IS NOT NULL AND [ClosedAt] IS NOT NULL AND [ClosedAt] >= [AssignedAt])");
                    table.CheckConstraint("CK_ServiceRequests_ValidPriority", "[Priority] IN (1, 2)");
                    table.CheckConstraint("CK_ServiceRequests_ValidRequestKey", "[RequestKey] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_ServiceRequests_ValidStatus", "[Status] IN (1, 2, 3)");
                    table.CheckConstraint("CK_ServiceRequests_ValidSubject", "[Subject] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Experts_AssignedExpertId",
                        column: x => x.AssignedExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertSpecialties_Subject_ExpertId",
                table: "ExpertSpecialties",
                columns: new[] { "Subject", "ExpertId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_AssignedExpertId_Status",
                table: "ServiceRequests",
                columns: new[] { "AssignedExpertId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_Status_CreatedOn_Id",
                table: "ServiceRequests",
                columns: new[] { "Status", "CreatedOn", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_ServiceRequests_RequestKey",
                table: "ServiceRequests",
                column: "RequestKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertSpecialties");

            migrationBuilder.DropTable(
                name: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "Experts");
        }
    }
}
