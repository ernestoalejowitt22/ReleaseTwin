using Microsoft.EntityFrameworkCore;
using ReleaseTwin.Hosted.Api.Data.Entities;

namespace ReleaseTwin.Hosted.Api.Data;

public sealed class HostedDbContext : DbContext
{
    public HostedDbContext(DbContextOptions<HostedDbContext> options) : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<UploadedCaseReport> UploadedCaseReports => Set<UploadedCaseReport>();
    public DbSet<UploadedFlagProofReport> UploadedFlagProofReports => Set<UploadedFlagProofReport>();
    public DbSet<Connection> Connections => Set<Connection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.HasMany(o => o.Projects).WithOne(p => p.Organization!).HasForeignKey(p => p.OrganizationId);
            e.HasMany(o => o.Users).WithOne(u => u.Organization!).HasForeignKey(u => u.OrganizationId);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.ClerkUserId).IsUnique();
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.HasMany(p => p.ApiTokens).WithOne(t => t.Project!).HasForeignKey(t => t.ProjectId);
            e.HasOne(p => p.Connection).WithOne(c => c.Project!).HasForeignKey<Connection>(c => c.ProjectId);
        });

        modelBuilder.Entity<Connection>(e =>
        {
            e.HasIndex(c => c.ProjectId).IsUnique();
        });

        modelBuilder.Entity<ApiToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
        });

        modelBuilder.Entity<UploadedCaseReport>(e =>
        {
            e.HasOne(r => r.Project).WithMany().HasForeignKey(r => r.ProjectId);
            e.HasIndex(r => r.ProjectId);
        });

        modelBuilder.Entity<UploadedFlagProofReport>(e =>
        {
            e.HasOne(r => r.Project).WithMany().HasForeignKey(r => r.ProjectId);
            e.HasIndex(r => r.ProjectId);
        });
    }
}
