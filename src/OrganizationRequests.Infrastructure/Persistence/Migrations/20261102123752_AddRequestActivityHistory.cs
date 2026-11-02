using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationRequests.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestActivityHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequestActivityHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceRequestId = table.Column<int>(type: "int", nullable: false),
                    ExpertId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ActorType = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestActivityHistories", x => x.Id);
                    table.CheckConstraint("CK_RequestActivityHistories_Actor", "([ActorType] = 1 AND [ActorUserId] IS NOT NULL AND [ActorUserId] <> 0) OR ([ActorType] = 2 AND [ActorUserId] IS NULL)");
                    table.CheckConstraint("CK_RequestActivityHistories_Transition", "([EventType] = 1 AND [PreviousStatus] IS NULL AND [NewStatus] = 1 AND [ExpertId] IS NULL) OR ([EventType] = 2 AND [PreviousStatus] IS NOT NULL AND [PreviousStatus] = 1 AND [NewStatus] = 2 AND [ExpertId] IS NOT NULL) OR ([EventType] = 3 AND [PreviousStatus] IS NOT NULL AND [PreviousStatus] = 2 AND [NewStatus] = 3 AND [ExpertId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RequestActivityHistories_Experts_ExpertId",
                        column: x => x.ExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestActivityHistories_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestActivityHistories_ExpertId_OccurredAt_Id",
                table: "RequestActivityHistories",
                columns: new[] { "ExpertId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestActivityHistories_RequestId_OccurredAt_Id",
                table: "RequestActivityHistories",
                columns: new[] { "ServiceRequestId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_RequestActivityHistories_RequestId_EventType",
                table: "RequestActivityHistories",
                columns: new[] { "ServiceRequestId", "EventType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestActivityHistories");
        }
    }
}
