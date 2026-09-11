using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileFormatAIStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRawExtractedTextToKnowledgebaseDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawExtractedText",
                table: "KnowledgebaseDocuments",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawExtractedText",
                table: "KnowledgebaseDocuments");
        }
    }
}
