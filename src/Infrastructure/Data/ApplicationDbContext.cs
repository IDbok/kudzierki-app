using Infrastructure.Entities;
using Infrastructure.Entities.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WorkDay> WorkDays => Set<WorkDay>();
    public DbSet<WorkDayAssignment> WorkDayAssignments => Set<WorkDayAssignment>();
    public DbSet<CashRegisterClosing> CashRegisterClosings => Set<CashRegisterClosing>();
    public DbSet<AltegioTransactionRaw> AltegioTransactionRaws => Set<AltegioTransactionRaw>();
    public DbSet<AltegioTransactionSnapshot> AltegioTransactionSnapshots => Set<AltegioTransactionSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).HasConversion<string>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL");

            entity.HasOne(e => e.User)
                .WithOne(u => u.Employee)
                .HasForeignKey<Employee>(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Token).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkDay>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.Date)
                .IsUnique();

            entity.HasMany(e => e.Assignments)
                .WithOne(a => a.WorkDay)
                .HasForeignKey(a => a.WorkDayId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkDayAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WorkDayId);
            entity.HasIndex(e => e.EmployeeId);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Portion).HasPrecision(3, 2);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashRegisterClosing>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.CashBalanceFact).HasPrecision(18, 2);
            entity.Property(e => e.TerminalIncomeFact).HasPrecision(18, 2);
            entity.Property(e => e.CashBalanceDayBefore).HasPrecision(18, 2);
            entity.Property(e => e.CashIncomeAltegio).HasPrecision(18, 2);
            entity.Property(e => e.TransferIncomeAltegio).HasPrecision(18, 2);
            entity.Property(e => e.TerminalIncomeAltegio).HasPrecision(18, 2);
            entity.Property(e => e.CashSpendingAdmin).HasPrecision(18, 2);
            entity.Property(e => e.InCashTransfer).HasPrecision(18, 2);
            entity.Property(e => e.OutCashTransfer).HasPrecision(18, 2);

            entity.Property(e => e.Comment)
                .HasMaxLength(2048);

            entity.HasIndex(e => e.Date)
                .IsUnique();
        });

        modelBuilder.Entity<AltegioTransactionRaw>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExternalId).IsRequired();

            entity.Property(e => e.PayloadJson)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.PayloadHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(e => e.FetchedAtUtc).IsRequired();

            entity.HasIndex(e => new { e.ExternalId, e.PayloadHash })
                .IsUnique();

            entity.HasIndex(e => e.FetchedAtUtc);
        });

        modelBuilder.Entity<AltegioTransactionSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExternalId).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);

            entity.Property(e => e.Comment)
                .HasMaxLength(2048);

            entity.Property(e => e.AccountTitle)
                .HasMaxLength(256);

            entity.Property(e => e.IsCash).IsRequired();
            entity.Property(e => e.FirstSeenAtUtc).IsRequired();
            entity.Property(e => e.LastSeenAtUtc).IsRequired();

            entity.HasIndex(e => e.ExternalId)
                .IsUnique();
        });
    }
}
