using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppHost.Persistence.Configurations;

public sealed class TagEntityConfiguration : IEntityTypeConfiguration<TagEntity>
{
    public void Configure(EntityTypeBuilder<TagEntity> entity)
    {
        entity.ToTable("tags");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Name).IsRequired();
        entity.Property(e => e.NormalizedName).IsRequired();
        entity.Property(e => e.IsSystem).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
        entity.Property(e => e.UpdatedAt).IsRequired();

        entity.HasIndex(e => e.NormalizedName).IsUnique();
    }
}
