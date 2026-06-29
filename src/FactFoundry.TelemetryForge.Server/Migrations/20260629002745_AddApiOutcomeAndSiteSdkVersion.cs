using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactFoundry.TelemetryForge.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddApiOutcomeAndSiteSdkVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastSdkVersion",
                table: "Sites",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "ApiEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSdkVersion",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "ApiEvents");
        }
    }
}
