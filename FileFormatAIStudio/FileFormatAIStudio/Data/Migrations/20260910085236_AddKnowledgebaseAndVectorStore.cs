using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileFormatAIStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgebaseAndVectorStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Knowledgebases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ParserEngine = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingProvider = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "TEXT", nullable: false),
                    VectorDimensions = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Knowledgebases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgebaseDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgebaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ParserEngineUsed = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ChunkCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgebaseDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgebaseDocuments_Knowledgebases_KnowledgebaseId",
                        column: x => x.KnowledgebaseId,
                        principalTable: "Knowledgebases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionKnowledgebases",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgebaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionKnowledgebases", x => new { x.SessionId, x.KnowledgebaseId });
                    table.ForeignKey(
                        name: "FK_SessionKnowledgebases_Knowledgebases_KnowledgebaseId",
                        column: x => x.KnowledgebaseId,
                        principalTable: "Knowledgebases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionKnowledgebases_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgebaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TextContent = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", nullable: false),
                    PageOrSectionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ChunkIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EmbeddingVector = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentChunks_KnowledgebaseDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "KnowledgebaseDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentChunks_Knowledgebases_KnowledgebaseId",
                        column: x => x.KnowledgebaseId,
                        principalTable: "Knowledgebases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_KnowledgebaseId",
                table: "DocumentChunks",
                column: "KnowledgebaseId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgebaseDocuments_KnowledgebaseId",
                table: "KnowledgebaseDocuments",
                column: "KnowledgebaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionKnowledgebases_KnowledgebaseId",
                table: "SessionKnowledgebases",
                column: "KnowledgebaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentChunks");

            migrationBuilder.DropTable(
                name: "SessionKnowledgebases");

            migrationBuilder.DropTable(
                name: "KnowledgebaseDocuments");

            migrationBuilder.DropTable(
                name: "Knowledgebases");
        }
    }
}
