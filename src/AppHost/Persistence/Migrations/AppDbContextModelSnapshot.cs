using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace AppHost.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.0");

        modelBuilder.Entity("AppHost.Persistence.RootEntity", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Path")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("Status")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("Path")
                .IsUnique();

            b.ToTable("roots");
        });

        modelBuilder.Entity("AppHost.Persistence.ScanSessionEntity", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("DiskKey")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<int?>("DepthLimit")
                .HasColumnType("INTEGER");

            b.Property<DateTimeOffset?>("FinishedAt")
                .HasColumnType("TEXT");

            b.Property<long>("FilesScanned")
                .HasColumnType("INTEGER");

            b.Property<string>("Mode")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string?>("CurrentPath")
                .HasColumnType("TEXT");

            b.Property<string?>("OutputPath")
                .HasColumnType("TEXT");

            b.Property<Guid?>("RootId")
                .HasColumnType("TEXT");

            b.Property<string>("RootPath")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset?>("StartedAt")
                .HasColumnType("TEXT");

            b.Property<string>("State")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<long?>("TotalFiles")
                .HasColumnType("INTEGER");

            b.HasKey("Id");

            b.ToTable("scan_sessions");
        });
    }
}
