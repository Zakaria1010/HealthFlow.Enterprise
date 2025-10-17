using HealthFlow.Services.Patients.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthFlow.Services.Patients.Data
{
    public class PatientDbContext : DbContext
    {
        public PatientDbContext(DbContextOptions<PatientDbContext> options) : base(options)
        {
        }

        public DbSet<Patient> Patients => Set<Patient>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Patient entity configuration
            modelBuilder.Entity<Patient>(entity =>
            {
                // Primary key
                entity.HasKey(p => p.Id);

                // Properties configuration
                entity.Property(p => p.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.LastName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(p => p.MedicalRecordNumber)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(p => p.DateOfBirth)
                    .IsRequired();

                entity.Property(p => p.Status)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(p => p.CreatedAt)
                    .IsRequired();

                entity.Property(p => p.UpdatedAt)
                    .IsRequired(false);

                // Indexes
                entity.HasIndex(p => p.MedicalRecordNumber)
                    .IsUnique()
                    .HasDatabaseName("IX_Patients_MedicalRecordNumber");

                entity.HasIndex(p => p.LastName)
                    .HasDatabaseName("IX_Patients_LastName");

                entity.HasIndex(p => p.Status)
                    .HasDatabaseName("IX_Patients_Status");

                entity.HasIndex(p => p.CreatedAt)
                    .HasDatabaseName("IX_Patients_CreatedAt");

                // Seed data for development
                if (Database.IsSqlServer())
                {
                    entity.HasData(
                        new Patient
                        {
                            Id = Guid.NewGuid(),
                            FirstName = "John",
                            LastName = "Doe",
                            DateOfBirth = new DateTime(1980, 1, 15),
                            MedicalRecordNumber = "MRN001",
                            Status = PatientStatus.Admitted,
                            CreatedAt = DateTime.UtcNow.AddDays(-2)
                        },
                        new Patient
                        {
                            Id = Guid.NewGuid(),
                            FirstName = "Jane",
                            LastName = "Smith",
                            DateOfBirth = new DateTime(1975, 6, 22),
                            MedicalRecordNumber = "MRN002",
                            Status = PatientStatus.InTreatment,
                            CreatedAt = DateTime.UtcNow.AddDays(-1)
                        },
                        new Patient
                        {
                            Id = Guid.NewGuid(),
                            FirstName = "Robert",
                            LastName = "Johnson",
                            DateOfBirth = new DateTime(1990, 3, 8),
                            MedicalRecordNumber = "MRN003",
                            Status = PatientStatus.Critical,
                            CreatedAt = DateTime.UtcNow.AddHours(-4)
                        }
                    );
                }
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Auto-set UpdatedAt for modified entities
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is Patient && 
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entityEntry in entries)
            {
                if (entityEntry.State == EntityState.Added)
                {
                    ((Patient)entityEntry.Entity).CreatedAt = DateTime.UtcNow;
                }
                else if (entityEntry.State == EntityState.Modified)
                {
                    ((Patient)entityEntry.Entity).UpdatedAt = DateTime.UtcNow;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}