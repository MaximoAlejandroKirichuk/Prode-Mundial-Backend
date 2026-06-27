using Api.Domain.Entities;
using Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<RegistrationPayment> RegistrationPayments => Set<RegistrationPayment>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<RegistrationAnomaly> RegistrationAnomalies => Set<RegistrationAnomaly>();

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

            entity.HasIndex(r => new { r.Email, r.TournamentId });

            entity.Property(r => r.TournamentId)
                .IsRequired();

            entity.HasOne(r => r.Tournament)
                .WithMany()
                .HasForeignKey(r => r.TournamentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.Payments)
                .WithOne()
                .HasForeignKey(p => p.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(r => r.Anomalies)
                .WithOne()
                .HasForeignKey(a => a.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(r => r.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(r => r.PaymentPreferenceId)
                .HasMaxLength(100);

            entity.Property(r => r.PaymentUrl)
                .HasMaxLength(500);

            entity.Property(r => r.AnomalyNote)
                .HasMaxLength(2000);

            entity.Property(r => r.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Active)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(t => t.PriceAmount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            entity.Property(t => t.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(t => t.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<RegistrationPayment>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.PaymentId)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(p => p.PaymentId);

            entity.Property(p => p.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            entity.Property(p => p.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(p => p.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(p => p.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.Property(d => d.PaymentId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(d => d.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(d => new { d.PaymentId, d.Status })
                .IsUnique();

            entity.Property(d => d.Topic)
                .HasMaxLength(50);

            entity.Property(d => d.Processed)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(d => d.ReceivedAt)
                .IsRequired();
        });

        modelBuilder.Entity<RegistrationAnomaly>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Type)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.HasIndex(a => a.RegistrationId);

            entity.Property(a => a.DetectedAt)
                .IsRequired();
        });
    }
}
