using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FactFoundry.TelemetryForge.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddApiEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteId = table.Column<string>(type: "text", nullable: false),
                    RouteTemplate = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: true),
                    CountryCode = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiEvents_RouteTemplate",
                table: "ApiEvents",
                column: "RouteTemplate");

            migrationBuilder.CreateIndex(
                name: "IX_ApiEvents_SiteId",
                table: "ApiEvents",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiEvents_StatusCode",
                table: "ApiEvents",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_ApiEvents_Timestamp",
                table: "ApiEvents",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiEvents");
        }
    }
}
