using Api.Domain.Entities;
using Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(r => r.Email)
                .IsRequired()
                .HasMaxLength(320);

            entity.HasIndex(r => r.Email);

            entity.Property(r => r.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(r => r.PaymentPreferenceId)
                .HasMaxLength(100);

            entity.Property(r => r.PaymentUrl)
                .HasMaxLength(500);

            entity.Property(r => r.CreatedAt)
                .IsRequired();
        });
    }
}
