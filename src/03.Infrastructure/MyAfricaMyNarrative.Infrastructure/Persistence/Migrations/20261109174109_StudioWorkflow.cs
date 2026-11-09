using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAfricaMyNarrative.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudioWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "MediaAssets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "MediaAssets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "MediaAssets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttemptToken",
                table: "Jobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAtUtc",
                table: "Jobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastFailure",
                table: "Jobs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAtUtc",
                table: "Jobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "Jobs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                table: "Jobs",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "PayloadJson",
                table: "Jobs",
                type: "nvarchar(max)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ExportArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Availability = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewApprovals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryRevisions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TranscriptRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartMs = table.Column<long>(type: "bigint", nullable: false),
                    EndMs = table.Column<long>(type: "bigint", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranscriptRevisions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryScenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TextOrigin = table.Column<int>(type: "int", nullable: false),
                    AudioAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisualAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TranscriptRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartMs = table.Column<long>(type: "bigint", nullable: false),
                    EndMs = table.Column<long>(type: "bigint", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryScenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryScenes_StoryRevisions_StoryRevisionId",
                        column: x => x.StoryRevisionId,
                        principalTable: "StoryRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportArtifacts_StorageKey",
                table: "ExportArtifacts",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceItems_ProjectId",
                table: "ResourceItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewApprovals_ProjectId_StoryRevisionId_SnapshotHash",
                table: "ReviewApprovals",
                columns: new[] { "ProjectId", "StoryRevisionId", "SnapshotHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoryRevisions_ProjectId_RevisionNumber",
                table: "StoryRevisions",
                columns: new[] { "ProjectId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoryScenes_StoryRevisionId_Order",
                table: "StoryScenes",
                columns: new[] { "StoryRevisionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptRevisions_ProjectId_AssetId_CreatedAtUtc",
                table: "TranscriptRevisions",
                columns: new[] { "ProjectId", "AssetId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportArtifacts");

            migrationBuilder.DropTable(
                name: "ResourceItems");

            migrationBuilder.DropTable(
                name: "ReviewApprovals");

            migrationBuilder.DropTable(
                name: "StoryScenes");

            migrationBuilder.DropTable(
                name: "TranscriptRevisions");

            migrationBuilder.DropTable(
                name: "StoryRevisions");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "AttemptToken",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LastFailure",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "PayloadJson",
                table: "Jobs");
        }
    }
}
