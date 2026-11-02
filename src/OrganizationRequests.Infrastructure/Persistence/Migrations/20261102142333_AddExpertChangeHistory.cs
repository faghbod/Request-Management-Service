using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationRequests.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertChangeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpertChangeHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpertId = table.Column<int>(type: "int", nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    OldName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NewName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OldCapacity = table.Column<int>(type: "int", nullable: true),
                    NewCapacity = table.Column<int>(type: "int", nullable: false),
                    OldIsActive = table.Column<bool>(type: "bit", nullable: true),
                    NewIsActive = table.Column<bool>(type: "bit", nullable: false),
                    OldSpecialtiesJson = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NewSpecialtiesJson = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertChangeHistories", x => x.Id);
                    table.CheckConstraint("CK_ExpertChangeHistories_Actor", "[ChangedByUserId] > 0");
                    table.CheckConstraint("CK_ExpertChangeHistories_Capacity", "[NewCapacity] > 0");
                    table.CheckConstraint("CK_ExpertChangeHistories_ChangeType", "[ChangeType] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ExpertChangeHistories_Experts_ExpertId",
                        column: x => x.ExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertChangeHistories_ExpertId_ChangedAtUtc_Id",
                table: "ExpertChangeHistories",
                columns: new[] { "ExpertId", "ChangedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertChangeHistories");
        }
    }
}
