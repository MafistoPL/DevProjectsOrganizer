using Microsoft.EntityFrameworkCore;
using System.IO;

namespace AppHost.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<RootEntity> Roots => Set<RootEntity>();
    public DbSet<ScanSessionEntity> ScanSessions => Set<ScanSessionEntity>();
    public DbSet<ProjectSuggestionEntity> ProjectSuggestions => Set<ProjectSuggestionEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<TagEntity> Tags => Set<TagEntity>();

    public static string GetDefaultDbPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dataDir = Path.Combine(appData, "DevProjectsOrganizer");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "data.db");
    }

    public static DbContextOptions<AppDbContext> CreateDefaultOptions()
    {
        var dbPath = GetDefaultDbPath();
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
