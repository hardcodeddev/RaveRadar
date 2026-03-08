using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaveRadar.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateArtistModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Artists",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Popularity",
                table: "Artists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TopTracks",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "Popularity",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "TopTracks",
                table: "Artists");
        }
    }
}
