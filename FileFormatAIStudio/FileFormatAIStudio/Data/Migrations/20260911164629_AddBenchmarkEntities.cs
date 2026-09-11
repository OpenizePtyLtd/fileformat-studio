using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileFormatAIStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarkEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenchmarkSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    TotalDocuments = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BenchmarkDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkDocuments_BenchmarkSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "BenchmarkSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BenchmarkRunResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EngineId = table.Column<string>(type: "TEXT", nullable: false),
                    EngineDisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ElapsedMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoryAllocatedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterCount = table.Column<long>(type: "INTEGER", nullable: false),
                    WordCount = table.Column<long>(type: "INTEGER", nullable: false),
                    OverallScore = table.Column<double>(type: "REAL", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtractedTextSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkRunResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkRunResults_BenchmarkDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "BenchmarkDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BenchmarkMetricResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetricId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    RawValue = table.Column<double>(type: "REAL", nullable: false),
                    FormattedValue = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedScore = table.Column<double>(type: "REAL", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    HigherIsBetter = table.Column<bool>(type: "INTEGER", nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkMetricResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkMetricResults_BenchmarkRunResults_RunResultId",
                        column: x => x.RunResultId,
                        principalTable: "BenchmarkRunResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkDocuments_SessionId",
                table: "BenchmarkDocuments",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkMetricResults_RunResultId",
                table: "BenchmarkMetricResults",
                column: "RunResultId");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRunResults_DocumentId",
                table: "BenchmarkRunResults",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenchmarkMetricResults");

            migrationBuilder.DropTable(
                name: "BenchmarkRunResults");

            migrationBuilder.DropTable(
                name: "BenchmarkDocuments");

            migrationBuilder.DropTable(
                name: "BenchmarkSessions");
        }
    }
}
