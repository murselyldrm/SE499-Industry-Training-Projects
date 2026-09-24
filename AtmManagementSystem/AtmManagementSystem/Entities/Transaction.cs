using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AtmManagementSystem.Entities;

public class Transaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AccountId { get; set; }

    [ForeignKey(nameof(AccountId))]
    public virtual Account Account { get; set; } = null!;

    public int? TargetAccountId { get; set; }

    [ForeignKey(nameof(TargetAccountId))]
    public virtual Account? TargetAccount { get; set; }

    [Required]
    public TransactionType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(250)]
    public string Description { get; set; } = string.Empty;
}