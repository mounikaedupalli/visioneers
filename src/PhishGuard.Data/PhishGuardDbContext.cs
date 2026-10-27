using Microsoft.EntityFrameworkCore;
using PhishGuard.Core.Domain;

namespace PhishGuard.Data;

public class PhishGuardDbContext(DbContextOptions<PhishGuardDbContext> options) : DbContext(options)
{
    public DbSet<ProtectedBrand> Brands => Set<ProtectedBrand>();
    public DbSet<UrlRecord> Urls => Set<UrlRecord>();
    public DbSet<DomainEvidence> DomainEvidence => Set<DomainEvidence>();
    public DbSet<UrlScore> Scores => Set<UrlScore>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<PhishCase> Cases => Set<PhishCase>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AnalystDecision> AnalystDecisions => Set<AnalystDecision>();
    public DbSet<EnforcementNotice> EnforcementNotices => Set<EnforcementNotice>();
    public DbSet<MonitorCheck> MonitorChecks => Set<MonitorCheck>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ProtectedBrand>(e =>
        {
            e.ToTable("Brands");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.PrimaryDomain).HasMaxLength(253);
            e.Property(x => x.Keywords).HasMaxLength(500);
            e.Property(x => x.OwnedDomains).HasMaxLength(2000);
            e.HasData(BrandCatalog.Seed);
        });

        b.Entity<UrlRecord>(e =>
        {
            e.ToTable("Urls");
            e.Property(x => x.Url).HasMaxLength(2048);
            e.Property(x => x.Host).HasMaxLength(253);
            e.Property(x => x.RegisteredDomain).HasMaxLength(253);
            e.Property(x => x.Tld).HasMaxLength(63);
            e.HasIndex(x => x.RegisteredDomain);
            e.HasIndex(x => new { x.Category, x.IsPhishing });
            e.HasOne(x => x.TargetBrand).WithMany().HasForeignKey(x => x.TargetBrandId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<DomainEvidence>(e =>
        {
            e.ToTable("DomainEvidence");
            e.HasKey(x => x.RegisteredDomain);
            e.Property(x => x.RegisteredDomain).HasMaxLength(253);
            e.Property(x => x.Registrar).HasMaxLength(200);
            e.Property(x => x.RegistrarAbuseEmail).HasMaxLength(254);
            e.Property(x => x.RegistrantCountry).HasMaxLength(2);
            e.Property(x => x.HostingProvider).HasMaxLength(200);
            e.Property(x => x.HostingAbuseEmail).HasMaxLength(254);
            e.Property(x => x.CertIssuer).HasMaxLength(200);
            e.Property(x => x.PageTitle).HasMaxLength(500);
            e.Property(x => x.ScreenshotRef).HasMaxLength(500);
        });

        b.Entity<UrlScore>(e =>
        {
            e.ToTable("UrlScores");
            e.Property(x => x.ModelVersion).HasMaxLength(50);
            e.HasIndex(x => new { x.ModelVersion, x.Band });
            // One score per URL per model version: a second Scoring worker can't create duplicates.
            e.HasIndex(x => new { x.UrlId, x.ModelVersion }).IsUnique();
            e.HasOne(x => x.Url).WithMany().HasForeignKey(x => x.UrlId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Campaign).WithMany(c => c.Scores).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Campaign>(e =>
        {
            e.ToTable("Campaigns");
            e.Property(x => x.GroupingKey).HasMaxLength(600);
            e.Property(x => x.RegisteredDomain).HasMaxLength(253);
            e.Property(x => x.PathPattern).HasMaxLength(300);
            e.HasIndex(x => x.GroupingKey).IsUnique();
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PhishCase>(e =>
        {
            e.ToTable("Cases");
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.AgentRuns).WithOne().HasForeignKey(x => x.CaseId);
            e.HasMany(x => x.Decisions).WithOne().HasForeignKey(x => x.CaseId);
            e.HasMany(x => x.Notices).WithOne().HasForeignKey(x => x.CaseId);
            e.HasMany(x => x.MonitorChecks).WithOne().HasForeignKey(x => x.CaseId);
        });

        b.Entity<AgentRun>(e => { e.ToTable("AgentRuns"); e.Property(x => x.AgentName).HasMaxLength(100); });
        b.Entity<AnalystDecision>(e => { e.ToTable("AnalystDecisions"); e.Property(x => x.Analyst).HasMaxLength(100); });
        b.Entity<EnforcementNotice>(e =>
        {
            e.ToTable("EnforcementNotices");
            e.Property(x => x.Recipient).HasMaxLength(254);
            e.Property(x => x.Subject).HasMaxLength(300);
        });
        b.Entity<MonitorCheck>(e => e.ToTable("MonitorChecks"));

        // Enums stored as strings: readable in SSMS and in agent tool output.
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties().Where(p => p.ClrType.IsEnum))
            {
                prop.SetProviderClrType(typeof(string));
                prop.SetMaxLength(40);
            }
    }
}
