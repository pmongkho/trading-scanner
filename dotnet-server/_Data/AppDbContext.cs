using TradingScanner._Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TradingScanner.Persistence.Entities;

namespace TradingScanner._Data;

public class AppDbContext
    : IdentityDbContext<User, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<TickerMetadata> TickerMetadata => Set<TickerMetadata>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<ScannerSignal> ScannerSignals => Set<ScannerSignal>();
    public DbSet<AlertHistory> AlertHistory => Set<AlertHistory>();
    public DbSet<HistoricalPerformance> HistoricalPerformance => Set<HistoricalPerformance>();
    public DbSet<ScannerConfiguration> ScannerConfigurations => Set<ScannerConfiguration>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("scanner");

        builder.Entity<TickerMetadata>(entity =>
        {
            entity.ToTable("ticker_metadata");
            entity.HasIndex(x => x.Symbol).IsUnique();
            entity.Property(x => x.Symbol).HasMaxLength(12);
        });
        builder.Entity<NewsArticle>(entity =>
        {
            entity.ToTable("news_articles");
            entity.HasIndex(x => x.ProviderId).IsUnique();
            entity.Property(x => x.Headline).HasMaxLength(500);
        });
        builder.Entity<ScannerSignal>(entity =>
        {
            entity.ToTable("scanner_signals");
            entity.HasIndex(x => new { x.Symbol, x.Timestamp });
            entity.Property(x => x.Price).HasPrecision(18, 6);
            entity.HasOne(x => x.Performance).WithOne(x => x.ScannerSignal)
                .HasForeignKey<HistoricalPerformance>(x => x.ScannerSignalId);
        });
        builder.Entity<AlertHistory>(entity =>
        {
            entity.ToTable("alert_history");
            entity.HasIndex(x => new { x.Symbol, x.Timestamp });
        });
        builder.Entity<HistoricalPerformance>().ToTable("historical_performance");
        builder.Entity<ScannerConfiguration>(entity =>
        {
            entity.ToTable("scanner_configuration");
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.JsonValue).HasColumnType("jsonb");
        });
    }
}
