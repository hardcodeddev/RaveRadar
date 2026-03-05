using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaveRadar.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEventsWithMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtistNames",
                table: "Events",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GenreNames",
                table: "Events",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtistNames",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "GenreNames",
                table: "Events");
        }
    }
}
