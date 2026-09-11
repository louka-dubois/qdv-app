using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QDVapp.Models;

namespace QDVapp.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProjetUsine> Projets { get; set; } = null!;
    public DbSet<ProjectCorrection> Corrections { get; set; } = null!;
    public DbSet<ManuelOF> ManuelOFs { get; set; } = null!;
    public DbSet<UploadedExcel> UploadedExcels { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ProjetUsine>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.UserId);
            entity.Property(p => p.DateRequise).HasColumnType("timestamp with time zone");
        });

        builder.Entity<ProjectCorrection>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.UserId, p.Page, p.ProjectKey, p.Field }).IsUnique();
            entity.Property(p => p.Value).HasMaxLength(500);
        });

        builder.Entity<ManuelOF>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.UserId);
            entity.Property(p => p.RequisFab).HasColumnType("timestamp with time zone");
            entity.Property(p => p.RequisFinal).HasColumnType("timestamp with time zone");
        });

        builder.Entity<UploadedExcel>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.UserId, p.Page }).IsUnique();
            entity.Property(p => p.Data).HasColumnType("bytea");
            entity.Property(p => p.UploadedAt).HasColumnType("timestamp with time zone");
        });
    }
}
