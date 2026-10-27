using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhishGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "BrandPageSimilarity",
                table: "DomainEvidence",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SandboxAcceptsAnyCredentials",
                table: "DomainEvidence",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SandboxPostTarget",
                table: "DomainEvidence",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SandboxPostsToExternalHost",
                table: "DomainEvidence",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SandboxRedirectsToBrand",
                table: "DomainEvidence",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SandboxRunAtUtc",
                table: "DomainEvidence",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Indicators",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Indicators", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Indicators_CaseId",
                table: "Indicators",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Indicators_Type_Value",
                table: "Indicators",
                columns: new[] { "Type", "Value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Indicators");

            migrationBuilder.DropColumn(
                name: "BrandPageSimilarity",
                table: "DomainEvidence");

            migrationBuilder.DropColumn(
                name: "SandboxAcceptsAnyCredentials",
                table: "DomainEvidence");

            migrationBuilder.DropColumn(
                name: "SandboxPostTarget",
                table: "DomainEvidence");

            migrationBuilder.DropColumn(
                name: "SandboxPostsToExternalHost",
                table: "DomainEvidence");

            migrationBuilder.DropColumn(
                name: "SandboxRedirectsToBrand",
                table: "DomainEvidence");

            migrationBuilder.DropColumn(
                name: "SandboxRunAtUtc",
                table: "DomainEvidence");
        }
    }
}
