using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAfricaMyNarrative.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorIntentAndFixtureProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatorIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Meaning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Tone = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocalContext = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    ProtectedTerms = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorIntents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudioProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    BaselineHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsFixture = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudioProposals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorIntents_ProjectId",
                table: "CreatorIntents",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudioProposals_ProjectId_CreatedAtUtc",
                table: "StudioProposals",
                columns: new[] { "ProjectId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatorIntents");

            migrationBuilder.DropTable(
                name: "StudioProposals");
        }
    }
}
