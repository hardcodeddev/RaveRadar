using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaveRadar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioFeaturesToSavedTrack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AudioFeaturesEnriched",
                table: "SavedTracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "BpmValue",
                table: "SavedTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "DanceabilityScore",
                table: "SavedTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "DarknessScore",
                table: "SavedTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "EnergyScore",
                table: "SavedTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "ValenceScore",
                table: "SavedTracks",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioFeaturesEnriched",
                table: "SavedTracks");

            migrationBuilder.DropColumn(
                name: "BpmValue",
                table: "SavedTracks");

            migrationBuilder.DropColumn(
                name: "DanceabilityScore",
                table: "SavedTracks");

            migrationBuilder.DropColumn(
                name: "DarknessScore",
                table: "SavedTracks");

            migrationBuilder.DropColumn(
                name: "EnergyScore",
                table: "SavedTracks");

            migrationBuilder.DropColumn(
                name: "ValenceScore",
                table: "SavedTracks");
        }
    }
}
