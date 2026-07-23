using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpillSense.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    InsertedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UnchangedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RejectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuarantinedRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    RawRow = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Reasons = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuarantinedRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuarantinedRecords_ImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "ImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRuns_StartedAtUtc",
                table: "ImportRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedRecords_ImportRunId",
                table: "QuarantinedRecords",
                column: "ImportRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuarantinedRecords");

            migrationBuilder.DropTable(
                name: "ImportRuns");
        }
    }
}
