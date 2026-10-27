using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhishGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueScorePerModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UrlScores_UrlId",
                table: "UrlScores");

            migrationBuilder.CreateIndex(
                name: "IX_UrlScores_UrlId_ModelVersion",
                table: "UrlScores",
                columns: new[] { "UrlId", "ModelVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UrlScores_UrlId_ModelVersion",
                table: "UrlScores");

            migrationBuilder.CreateIndex(
                name: "IX_UrlScores_UrlId",
                table: "UrlScores",
                column: "UrlId");
        }
    }
}
