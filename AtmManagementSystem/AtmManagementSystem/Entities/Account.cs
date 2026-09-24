using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AtmManagementSystem.Entities;

public class Account
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string CardHolderName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string PinHash { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    public int FailedLoginAttempts { get; set; } = 0;

    public bool IsBlocked { get; set; } = false;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyWithdrawalLimit { get; set; } = 1000.00m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}