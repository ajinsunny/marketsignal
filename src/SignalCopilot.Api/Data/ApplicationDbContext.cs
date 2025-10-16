using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Holding> Holdings { get; set; }
    public DbSet<Article> Articles { get; set; }
    public DbSet<Signal> Signals { get; set; }
    public DbSet<Impact> Impacts { get; set; }
    public DbSet<Alert> Alerts { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Holding configuration
        builder.Entity<Holding>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.HasIndex(h => new { h.UserId, h.Ticker }).IsUnique();
            entity.Property(h => h.Shares).HasPrecision(18, 8);
            entity.Property(h => h.CostBasis).HasPrecision(18, 2);

            entity.HasOne(h => h.User)
                .WithMany(u => u.Holdings)
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Article configuration
        builder.Entity<Article>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Ticker);
            entity.HasIndex(a => a.PublishedAt);
        });

        // Signal configuration
        builder.Entity<Signal>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.ArticleId).IsUnique();
            entity.Property(s => s.Confidence).HasPrecision(5, 2);

            entity.HasOne(s => s.Article)
                .WithOne(a => a.Signal)
                .HasForeignKey<Signal>(s => s.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Impact configuration
        builder.Entity<Impact>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => new { i.UserId, i.ArticleId });
            entity.HasIndex(i => i.ImpactScore);
            entity.Property(i => i.ImpactScore).HasPrecision(10, 4);
            entity.Property(i => i.Exposure).HasPrecision(5, 4);

            entity.HasOne(i => i.User)
                .WithMany(u => u.Impacts)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Article)
                .WithMany(a => a.Impacts)
                .HasForeignKey(i => i.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Holding)
                .WithMany(h => h.Impacts)
                .HasForeignKey(i => i.HoldingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Alert configuration
        builder.Entity<Alert>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.UserId, a.CreatedAt });
            entity.HasIndex(a => a.Status);

            entity.HasOne(a => a.User)
                .WithMany(u => u.Alerts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
