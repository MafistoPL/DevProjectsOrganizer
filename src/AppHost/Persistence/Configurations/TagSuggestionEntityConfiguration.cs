using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppHost.Persistence.Configurations;

public sealed class TagSuggestionEntityConfiguration : IEntityTypeConfiguration<TagSuggestionEntity>
{
    public void Configure(EntityTypeBuilder<TagSuggestionEntity> entity)
    {
        entity.ToTable("tag_suggestions");
        entity.HasKey(item => item.Id);

        entity.Property(item => item.ProjectId).IsRequired();
        entity.Property(item => item.TagId);
        entity.Property(item => item.SuggestedTagName).IsRequired();
        entity.Property(item => item.Type).IsRequired().HasConversion<string>();
        entity.Property(item => item.Source).IsRequired().HasConversion<string>();
        entity.Property(item => item.Confidence).IsRequired();
        entity.Property(item => item.Reason).IsRequired();
        entity.Property(item => item.Fingerprint).IsRequired();
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.Property(item => item.Status).IsRequired().HasConversion<string>();

        entity.HasIndex(item => item.ProjectId);
        entity.HasIndex(item => item.Status);
        entity.HasIndex(item => item.CreatedAt);

        entity
            .HasOne<ProjectEntity>()
            .WithMany()
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity
            .HasOne<TagEntity>()
            .WithMany()
            .HasForeignKey(item => item.TagId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
