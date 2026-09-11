using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileFormatAIStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCitationJsonToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitationJson",
                table: "Messages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CitationJson",
                table: "Messages");
        }
    }
}
