using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppHost.Persistence.Configurations;

public sealed class ProjectSuggestionEntityConfiguration : IEntityTypeConfiguration<ProjectSuggestionEntity>
{
    public void Configure(EntityTypeBuilder<ProjectSuggestionEntity> entity)
    {
        entity.ToTable("project_suggestions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ScanSessionId).IsRequired();
        entity.Property(e => e.RootPath).IsRequired();
        entity.Property(e => e.Name).IsRequired();
        entity.Property(e => e.Path).IsRequired();
        entity.Property(e => e.Kind).IsRequired();
        entity.Property(e => e.Score).IsRequired();
        entity.Property(e => e.Reason).IsRequired();
        entity.Property(e => e.ExtensionsSummary).IsRequired();
        entity.Property(e => e.Fingerprint).IsRequired();
        entity.Property(e => e.MarkersJson).IsRequired();
        entity.Property(e => e.TechHintsJson).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
        entity.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired();
        entity.HasIndex(e => e.ScanSessionId);
        entity.HasIndex(e => e.RootPath);
        entity.HasIndex(e => e.Path);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => new { e.Path, e.Kind, e.Fingerprint });
    }
}
