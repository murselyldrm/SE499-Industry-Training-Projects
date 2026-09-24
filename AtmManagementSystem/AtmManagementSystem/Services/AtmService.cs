using AtmManagementSystem.Data;
using AtmManagementSystem.Entities;
using AtmManagementSystem.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AtmManagementSystem.Services;

public class AtmService
{
    private readonly AtmDbContext _context;

    public AtmService(AtmDbContext context)
    {
        _context = context;
    }

    public async Task<Account> ValidateLoginAsync(string accountNumber, string rawPin)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber)
            ?? throw new AccountNotFoundException($"Account '{accountNumber}' was not found.");

        if (account.IsBlocked)
        {
            throw new AccountLockedException("Account is locked (IsBlocked = true) due to 3 failed PIN attempts.");
        }

        var inputPinHash = AtmDbContext.HashPin(rawPin);

        if (account.PinHash != inputPinHash)
        {
            account.FailedLoginAttempts++;

            if (account.FailedLoginAttempts >= 3)
            {
                account.IsBlocked = true;
                await _context.SaveChangesAsync();
                throw new AccountLockedException("Invalid PIN. Account has reached 3 failed attempts and is now locked (IsBlocked = true).");
            }

            await _context.SaveChangesAsync();
            throw new InvalidPinException($"Invalid PIN. Attempt {account.FailedLoginAttempts} of 3.");
        }

        account.FailedLoginAttempts = 0;
        await _context.SaveChangesAsync();

        return account;
    }

    public async Task<decimal> GetBalanceAsync(int accountId)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId)
            ?? throw new AccountNotFoundException($"Account with ID {accountId} was not found.");

        return account.Balance;
    }

    public async Task DepositAsync(int accountId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Deposit amount must be positive.", nameof(amount));
        }

        var account = await _context.Accounts.FindAsync(accountId)
            ?? throw new AccountNotFoundException($"Account with ID {accountId} was not found.");

        account.Balance += amount;

        var transaction = new Transaction
        {
            AccountId = account.Id,
            Type = TransactionType.Deposit,
            Amount = amount,
            Timestamp = DateTime.UtcNow,
            Description = $"Cash deposit of {amount:C2}"
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task WithdrawAsync(int accountId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Withdrawal amount must be positive.", nameof(amount));
        }

        var account = await _context.Accounts.FindAsync(accountId)
            ?? throw new AccountNotFoundException($"Account with ID {accountId} was not found.");

        if (account.Balance < amount)
        {
            throw new InsufficientFundsException($"Withdrawal rejected: Insufficient balance. Available: {account.Balance:C2}, Requested: {amount:C2}.");
        }

        var todayUtc = DateTime.UtcNow.Date;
        var totalWithdrawnToday = await _context.Transactions
            .Where(t => t.AccountId == accountId &&
                        t.Type == TransactionType.Withdrawal &&
                        t.Timestamp >= todayUtc)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        if (totalWithdrawnToday + amount > account.DailyWithdrawalLimit)
        {
            var remainingQuota = account.DailyWithdrawalLimit - totalWithdrawnToday;
            throw new DailyLimitExceededException(
                $"Withdrawal rejected: Exceeds daily limit of {account.DailyWithdrawalLimit:C2}. Remaining quota for today: {Math.Max(0, remainingQuota):C2}.");
        }

        account.Balance -= amount;

        var transaction = new Transaction
        {
            AccountId = account.Id,
            Type = TransactionType.Withdrawal,
            Amount = amount,
            Timestamp = DateTime.UtcNow,
            Description = $"Cash withdrawal of {amount:C2}"
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task TransferFundsAsync(int sourceAccountId, string targetAccountNumber, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Transfer amount must be positive.", nameof(amount));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var sourceAccount = await _context.Accounts.FindAsync(sourceAccountId)
                ?? throw new AccountNotFoundException($"Source account with ID {sourceAccountId} was not found.");

            var targetAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == targetAccountNumber)
                ?? throw new AccountNotFoundException($"Target account '{targetAccountNumber}' was not found.");

            if (sourceAccount.Id == targetAccount.Id)
            {
                throw new InvalidOperationException("Transfer failed: Source and destination accounts cannot be the same.");
            }

            if (sourceAccount.Balance < amount)
            {
                throw new InsufficientFundsException($"Transfer rejected: Insufficient funds. Available: {sourceAccount.Balance:C2}, Required: {amount:C2}.");
            }

            sourceAccount.Balance -= amount;
            targetAccount.Balance += amount;

            var transferRecord = new Transaction
            {
                AccountId = sourceAccount.Id,
                TargetAccountId = targetAccount.Id,
                Type = TransactionType.Transfer,
                Amount = amount,
                Timestamp = DateTime.UtcNow,
                Description = $"Transferred to {targetAccount.AccountNumber} ({targetAccount.CardHolderName})"
            };

            _context.Transactions.Add(transferRecord);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SimulateFailedTransferAsync(int sourceAccountId, string targetAccountNumber, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Transfer amount must be positive.", nameof(amount));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var sourceAccount = await _context.Accounts.FindAsync(sourceAccountId)
                ?? throw new AccountNotFoundException($"Source account with ID {sourceAccountId} was not found.");

            var targetAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == targetAccountNumber)
                ?? throw new AccountNotFoundException($"Target account '{targetAccountNumber}' was not found.");

            if (sourceAccount.Balance < amount)
            {
                throw new InsufficientFundsException($"Transfer rejected: Insufficient funds. Available: {sourceAccount.Balance:C2}, Required: {amount:C2}.");
            }

            sourceAccount.Balance -= amount;
            await _context.SaveChangesAsync();

            throw new InvalidOperationException("CRITICAL FAILURE: Network link severed mid-transfer. Initiating ACID transaction rollback...");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}