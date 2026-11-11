using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAfricaMyNarrative.Infrastructure.Persistence
{
    /// <inheritdoc />
    public partial class AddPilotFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApprovedOnce",
                table: "ProductionPlan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PilotFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntentMatch = table.Column<int>(type: "int", nullable: false),
                    NeededHelp = table.Column<int>(type: "int", nullable: false),
                    HelpNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PilotFeedback_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PilotFeedback_ProjectId",
                table: "PilotFeedback",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PilotFeedback");

            migrationBuilder.DropColumn(
                name: "ApprovedOnce",
                table: "ProductionPlan");
        }
    }
}
