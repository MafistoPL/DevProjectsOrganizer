using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppHost.Persistence.Configurations;

public sealed class ScanSessionEntityConfiguration : IEntityTypeConfiguration<ScanSessionEntity>
{
    public void Configure(EntityTypeBuilder<ScanSessionEntity> entity)
    {
        entity.ToTable("scan_sessions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.RootPath).IsRequired();
        entity.Property(e => e.Mode).IsRequired();
        entity.Property(e => e.State).IsRequired();
        entity.Property(e => e.DiskKey).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
    }
}
