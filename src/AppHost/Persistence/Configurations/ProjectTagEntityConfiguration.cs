using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppHost.Persistence.Configurations;

public sealed class ProjectTagEntityConfiguration : IEntityTypeConfiguration<ProjectTagEntity>
{
    public void Configure(EntityTypeBuilder<ProjectTagEntity> entity)
    {
        entity.ToTable("project_tags");
        entity.HasKey(item => new { item.ProjectId, item.TagId });

        entity.Property(item => item.ProjectId).IsRequired();
        entity.Property(item => item.TagId).IsRequired();
        entity.Property(item => item.CreatedAt).IsRequired();

        entity.HasIndex(item => item.TagId);

        entity
            .HasOne<ProjectEntity>()
            .WithMany()
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity
            .HasOne<TagEntity>()
            .WithMany()
            .HasForeignKey(item => item.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
