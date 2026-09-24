using System.Security.Cryptography;
using System.Text;
using AtmManagementSystem.Entities;
using Microsoft.EntityFrameworkCore;

namespace AtmManagementSystem.Data;

public class AtmDbContext : DbContext
{
    public AtmDbContext()
    {
    }

    public AtmDbContext(DbContextOptions<AtmDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AtmManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.AccountNumber).IsUnique();

            entity.Property(a => a.AccountNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(a => a.CardHolderName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.PinHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(a => a.Balance)
                .HasPrecision(18, 2);

            entity.Property(a => a.DailyWithdrawalLimit)
                .HasPrecision(18, 2);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Amount)
                .HasPrecision(18, 2);

            entity.Property(t => t.Description)
                .HasMaxLength(250);

            entity.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.TargetAccount)
                .WithMany()
                .HasForeignKey(t => t.TargetAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        var pin1Hash = HashPin("1234");
        var pin2Hash = HashPin("4321");

        modelBuilder.Entity<Account>().HasData(
            new Account
            {
                Id = 1,
                AccountNumber = "1001-2002-3003",
                CardHolderName = "Mürsel Yıldırım",
                PinHash = pin1Hash,
                Balance = 5000.00m,
                FailedLoginAttempts = 0,
                IsBlocked = false,
                DailyWithdrawalLimit = 1500.00m,
                CreatedAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
            },
            new Account
            {
                Id = 2,
                AccountNumber = "4004-5005-6006",
                CardHolderName = "Fikret Gözütok",
                PinHash = pin2Hash,
                Balance = 3200.00m,
                FailedLoginAttempts = 0,
                IsBlocked = false,
                DailyWithdrawalLimit = 2000.00m,
                CreatedAt = new DateTime(2026, 8, 1, 9, 30, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<Transaction>().HasData(
            new Transaction
            {
                Id = 1,
                AccountId = 1,
                Type = TransactionType.Deposit,
                Amount = 5000.00m,
                Timestamp = new DateTime(2026, 8, 1, 9, 15, 0, DateTimeKind.Utc),
                Description = "Initial Account Opening Deposit"
            },
            new Transaction
            {
                Id = 2,
                AccountId = 2,
                Type = TransactionType.Deposit,
                Amount = 3200.00m,
                Timestamp = new DateTime(2026, 8, 1, 9, 45, 0, DateTimeKind.Utc),
                Description = "Initial Account Opening Deposit"
            }
        );
    }

    public static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToHexString(bytes).ToLower();
    }
}