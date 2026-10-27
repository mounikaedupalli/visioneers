using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhishGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShortLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShortLinks",
                columns: table => new
                {
                    ShortUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExpandedUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ExpandedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortLinks", x => x.ShortUrl);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShortLinks");
        }
    }
}
