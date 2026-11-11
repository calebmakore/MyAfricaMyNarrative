using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAfricaMyNarrative.Infrastructure.Persistence
{
    /// <inheritdoc />
    public partial class AddMediaHasSound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasSound",
                table: "MediaAssets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Audio sources always carry sound (MediaKind.Audio = 1). Earlier videos stay picture-only until re-uploaded,
            // because whether they have a sound track was not recorded.
            migrationBuilder.Sql("UPDATE MediaAssets SET HasSound = 1 WHERE Kind = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasSound",
                table: "MediaAssets");
        }
    }
}
