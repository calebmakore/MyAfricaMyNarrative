using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAfricaMyNarrative.Infrastructure.Persistence
{
    /// <inheritdoc />
    public partial class AddProductionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Minutes",
                table: "ResourceItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductionPlan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    StoryRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResourceHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResourceSnapshot = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    TimeBudgetMinutes = table.Column<int>(type: "int", nullable: true),
                    PlannedShootMinutes = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    ChangeNote = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsFixture = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionPlan_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionPlanStep",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SceneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SceneTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Approach = table.Column<int>(type: "int", nullable: false),
                    Instruction = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    OnScreenSeconds = table.Column<int>(type: "int", nullable: false),
                    ShootMinutes = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ResourceIdList = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    ExistingAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionPlanStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionPlanStep_ProductionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "ProductionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_ProjectId_Version",
                table: "ProductionPlan",
                columns: new[] { "ProjectId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanStep_PlanId_Order",
                table: "ProductionPlanStep",
                columns: new[] { "PlanId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionPlanStep");

            migrationBuilder.DropTable(
                name: "ProductionPlan");

            migrationBuilder.DropColumn(
                name: "Minutes",
                table: "ResourceItems");
        }
    }
}
