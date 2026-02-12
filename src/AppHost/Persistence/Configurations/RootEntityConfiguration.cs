using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppHost.Persistence.Configurations;

public sealed class RootEntityConfiguration : IEntityTypeConfiguration<RootEntity>
{
    public void Configure(EntityTypeBuilder<RootEntity> entity)
    {
        entity.ToTable("roots");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Path).IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
        entity.HasIndex(e => e.Path).IsUnique();
    }
}
