using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAfricaMyNarrative.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LanguageReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReviewerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FluencyBasis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsSelfReview = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguageReviews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LanguageReviews_ProjectId_StoryRevisionId_SnapshotHash",
                table: "LanguageReviews",
                columns: new[] { "ProjectId", "StoryRevisionId", "SnapshotHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LanguageReviews");
        }
    }
}
