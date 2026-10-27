using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PhishGuard.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    Keywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OwnedDomains = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainEvidence",
                columns: table => new
                {
                    RegisteredDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Registrar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegistrarAbuseEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    RegistrantCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PrivacyProtected = table.Column<bool>(type: "bit", nullable: false),
                    HostingProvider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HostingAbuseEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    CertIssuer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CertIssuedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PageTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasLoginForm = table.Column<bool>(type: "bit", nullable: false),
                    BrandLogoDetected = table.Column<bool>(type: "bit", nullable: false),
                    ScreenshotRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainEvidence", x => x.RegisteredDomain);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupingKey = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    RegisteredDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    PathPattern = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BrandId = table.Column<int>(type: "int", nullable: true),
                    UrlCount = table.Column<int>(type: "int", nullable: false),
                    MaxProbability = table.Column<float>(type: "real", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Urls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Host = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    RegisteredDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    Tld = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsPhishing = table.Column<bool>(type: "bit", nullable: false),
                    TargetBrandId = table.Column<int>(type: "int", nullable: true),
                    IngestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Urls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Urls_Brands_TargetBrandId",
                        column: x => x.TargetBrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Verdict = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Confidence = table.Column<float>(type: "real", nullable: true),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cases_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UrlScores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UrlId = table.Column<long>(type: "bigint", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Probability = table.Column<float>(type: "real", nullable: false),
                    Band = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CampaignId = table.Column<long>(type: "bigint", nullable: true),
                    ScoredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrlScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UrlScores_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UrlScores_Urls_UrlId",
                        column: x => x.UrlId,
                        principalTable: "Urls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    AgentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Input = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Output = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokensUsed = table.Column<int>(type: "int", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuns_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalystDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    Analyst = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalystDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalystDecisions_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnforcementNotices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    Route = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FiledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnforcementNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnforcementNotices_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonitorChecks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitorChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitorChecks_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Brands",
                columns: new[] { "Id", "Keywords", "Name", "OwnedDomains", "PrimaryDomain" },
                values: new object[,]
                {
                    { 1, "paypal", "PayPal", "paypal.com,paypal.me,paypalobjects.com", "paypal.com" },
                    { 2, "microsoft,office365,outlook,onedrive,msft", "Microsoft", "microsoft.com,microsoftonline.com,office.com,live.com,outlook.com,sharepoint.com,onedrive.live.com", "microsoft.com" },
                    { 3, "apple,icloud,appleid", "Apple", "apple.com,icloud.com", "apple.com" },
                    { 4, "amazon,aws,prime", "Amazon", "amazon.com,amazonaws.com,media-amazon.com", "amazon.com" },
                    { 5, "netflix", "Netflix", "netflix.com,nflxext.com", "netflix.com" },
                    { 6, "google,gmail", "Google", "google.com,gmail.com,googleusercontent.com,gstatic.com", "google.com" },
                    { 7, "chase,jpmorgan", "Chase", "chase.com,jpmorganchase.com", "chase.com" },
                    { 8, "wellsfargo,wells-fargo", "Wells Fargo", "wellsfargo.com", "wellsfargo.com" },
                    { 9, "bankofamerica,bofa", "Bank of America", "bankofamerica.com", "bankofamerica.com" },
                    { 10, "dhl", "DHL", "dhl.com", "dhl.com" },
                    { 11, "fedex", "FedEx", "fedex.com", "fedex.com" },
                    { 12, "linkedin", "LinkedIn", "linkedin.com,licdn.com", "linkedin.com" },
                    { 13, "facebook,meta,instagram", "Facebook", "facebook.com,fb.com,instagram.com,meta.com", "facebook.com" },
                    { 14, "dropbox", "Dropbox", "dropbox.com", "dropbox.com" },
                    { 15, "docusign", "DocuSign", "docusign.com,docusign.net", "docusign.com" },
                    { 16, "adobe", "Adobe", "adobe.com", "adobe.com" },
                    { 17, "coinbase", "Coinbase", "coinbase.com", "coinbase.com" },
                    { 18, "usps", "USPS", "usps.com", "usps.com" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_CaseId",
                table: "AgentRuns",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalystDecisions_CaseId",
                table: "AnalystDecisions",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_BrandId",
                table: "Campaigns",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_GroupingKey",
                table: "Campaigns",
                column: "GroupingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CampaignId",
                table: "Cases",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Status",
                table: "Cases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EnforcementNotices_CaseId",
                table: "EnforcementNotices",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitorChecks_CaseId",
                table: "MonitorChecks",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Urls_Category_IsPhishing",
                table: "Urls",
                columns: new[] { "Category", "IsPhishing" });

            migrationBuilder.CreateIndex(
                name: "IX_Urls_RegisteredDomain",
                table: "Urls",
                column: "RegisteredDomain");

            migrationBuilder.CreateIndex(
                name: "IX_Urls_TargetBrandId",
                table: "Urls",
                column: "TargetBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_UrlScores_CampaignId",
                table: "UrlScores",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_UrlScores_ModelVersion_Band",
                table: "UrlScores",
                columns: new[] { "ModelVersion", "Band" });

            migrationBuilder.CreateIndex(
                name: "IX_UrlScores_UrlId",
                table: "UrlScores",
                column: "UrlId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRuns");

            migrationBuilder.DropTable(
                name: "AnalystDecisions");

            migrationBuilder.DropTable(
                name: "DomainEvidence");

            migrationBuilder.DropTable(
                name: "EnforcementNotices");

            migrationBuilder.DropTable(
                name: "MonitorChecks");

            migrationBuilder.DropTable(
                name: "UrlScores");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "Urls");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "Brands");
        }
    }
}
