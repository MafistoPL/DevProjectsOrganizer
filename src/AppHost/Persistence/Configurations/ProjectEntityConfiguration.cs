using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppHost.Persistence.Configurations;

public sealed class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    public void Configure(EntityTypeBuilder<ProjectEntity> entity)
    {
        entity.ToTable("projects");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.SourceSuggestionId).IsRequired();
        entity.Property(e => e.LastScanSessionId).IsRequired();
        entity.Property(e => e.RootPath).IsRequired();
        entity.Property(e => e.Name).IsRequired();
        entity.Property(e => e.Path).IsRequired();
        entity.Property(e => e.Kind).IsRequired();
        entity.Property(e => e.ProjectKey).IsRequired();
        entity.Property(e => e.Score).IsRequired();
        entity.Property(e => e.Reason).IsRequired();
        entity.Property(e => e.ExtensionsSummary).IsRequired();
        entity.Property(e => e.MarkersJson).IsRequired();
        entity.Property(e => e.TechHintsJson).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
        entity.Property(e => e.UpdatedAt).IsRequired();

        entity.HasIndex(e => e.ProjectKey).IsUnique();
        entity.HasIndex(e => e.RootPath);
        entity.HasIndex(e => e.Path);
    }
}
