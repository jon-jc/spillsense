using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SpillSense.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Counties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    FipsCode = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsCoastal = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpillIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ReportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    LocationDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    WaterbodyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CountyId = table.Column<int>(type: "INTEGER", nullable: true),
                    Medium = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubstanceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SubstanceCategory = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    QuantityGallons = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    RecoveredGallons = table.Column<decimal>(type: "TEXT", precision: 14, scale: 2, nullable: true),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ResponsibleParty = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpillIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpillIncidents_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Counties",
                columns: new[] { "Id", "FipsCode", "IsCoastal", "Name", "Region" },
                values: new object[,]
                {
                    { 1, "53001", false, "Adams", "Eastern" },
                    { 2, "53003", false, "Asotin", "Eastern" },
                    { 3, "53005", false, "Benton", "Central" },
                    { 4, "53007", false, "Chelan", "Central" },
                    { 5, "53009", true, "Clallam", "Southwest" },
                    { 6, "53011", false, "Clark", "Southwest" },
                    { 7, "53013", false, "Columbia", "Eastern" },
                    { 8, "53015", false, "Cowlitz", "Southwest" },
                    { 9, "53017", false, "Douglas", "Central" },
                    { 10, "53019", false, "Ferry", "Eastern" },
                    { 11, "53021", false, "Franklin", "Eastern" },
                    { 12, "53023", false, "Garfield", "Eastern" },
                    { 13, "53025", false, "Grant", "Eastern" },
                    { 14, "53027", true, "Grays Harbor", "Southwest" },
                    { 15, "53029", true, "Island", "Northwest" },
                    { 16, "53031", true, "Jefferson", "Southwest" },
                    { 17, "53033", true, "King", "Northwest" },
                    { 18, "53035", true, "Kitsap", "Northwest" },
                    { 19, "53037", false, "Kittitas", "Central" },
                    { 20, "53039", false, "Klickitat", "Central" },
                    { 21, "53041", false, "Lewis", "Southwest" },
                    { 22, "53043", false, "Lincoln", "Eastern" },
                    { 23, "53045", true, "Mason", "Southwest" },
                    { 24, "53047", false, "Okanogan", "Central" },
                    { 25, "53049", true, "Pacific", "Southwest" },
                    { 26, "53051", false, "Pend Oreille", "Eastern" },
                    { 27, "53053", true, "Pierce", "Southwest" },
                    { 28, "53055", true, "San Juan", "Northwest" },
                    { 29, "53057", true, "Skagit", "Northwest" },
                    { 30, "53059", false, "Skamania", "Southwest" },
                    { 31, "53061", true, "Snohomish", "Northwest" },
                    { 32, "53063", false, "Spokane", "Eastern" },
                    { 33, "53065", false, "Stevens", "Eastern" },
                    { 34, "53067", true, "Thurston", "Southwest" },
                    { 35, "53069", false, "Wahkiakum", "Southwest" },
                    { 36, "53071", false, "Walla Walla", "Eastern" },
                    { 37, "53073", true, "Whatcom", "Northwest" },
                    { 38, "53075", false, "Whitman", "Eastern" },
                    { 39, "53077", false, "Yakima", "Central" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Counties_FipsCode",
                table: "Counties",
                column: "FipsCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counties_Name",
                table: "Counties",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpillIncidents_CountyId",
                table: "SpillIncidents",
                column: "CountyId");

            migrationBuilder.CreateIndex(
                name: "IX_SpillIncidents_Latitude_Longitude",
                table: "SpillIncidents",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_SpillIncidents_ReportNumber",
                table: "SpillIncidents",
                column: "ReportNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpillIncidents_ReportedAtUtc",
                table: "SpillIncidents",
                column: "ReportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SpillIncidents_Status",
                table: "SpillIncidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SpillIncidents_SubstanceCategory",
                table: "SpillIncidents",
                column: "SubstanceCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpillIncidents");

            migrationBuilder.DropTable(
                name: "Counties");
        }
    }
}
