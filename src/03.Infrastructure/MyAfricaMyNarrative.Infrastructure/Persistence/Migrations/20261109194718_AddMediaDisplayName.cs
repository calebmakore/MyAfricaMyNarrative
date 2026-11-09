using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAfricaMyNarrative.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "MediaAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE MediaAssets SET DisplayName = OriginalName WHERE DisplayName = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "MediaAssets");
        }
    }
}
