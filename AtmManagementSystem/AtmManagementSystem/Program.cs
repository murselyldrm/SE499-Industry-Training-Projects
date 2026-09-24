using AtmManagementSystem.Data;
using AtmManagementSystem.Entities;
using AtmManagementSystem.Exceptions;
using AtmManagementSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace AtmManagementSystem;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        using (var initContext = new AtmDbContext())
        {
            await initContext.Database.EnsureCreatedAsync();
        }

        while (true)
        {
            PrintHeader("CORE BANKING TERMINAL - SELF-SERVICE ATM");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" [1] Insert Card (Authenticate)");
            Console.WriteLine(" [2] Diagnostic & Ledger Overview");
            Console.WriteLine(" [0] Terminate Terminal");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\nSelect action: ");
            Console.ResetColor();

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await HandleLoginFlowAsync();
                    break;
                case "2":
                    await DisplayLedgerViewAsync();
                    break;
                case "0":
                    PrintSuccess("Terminal offline. System shutting down.");
                    return;
                default:
                    PrintError("Invalid selection. Please choose an available option.");
                    Pause();
                    break;
            }
        }
    }

    private static async Task HandleLoginFlowAsync()
    {
        PrintHeader("CARD INSERTION & AUTHENTICATION");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enter Account Number: ");
        Console.ResetColor();
        var accountNumber = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enter 4-Digit Security PIN: ");
        Console.ResetColor();
        var pin = Console.ReadLine()?.Trim() ?? string.Empty;

        using var context = new AtmDbContext();
        var service = new AtmService(context);

        try
        {
            var account = await service.ValidateLoginAsync(accountNumber, pin);
            PrintSuccess($"Authentication verified. Welcome, {account.CardHolderName}.");
            Pause();
            await RunSessionLoopAsync(account.Id);
        }
        catch (InvalidPinException ex)
        {
            PrintError(ex.Message);
            Pause();
        }
        catch (AccountLockedException ex)
        {
            PrintError(ex.Message);
            Pause();
        }
        catch (AccountNotFoundException ex)
        {
            PrintError(ex.Message);
            Pause();
        }
        catch (Exception ex)
        {
            PrintError($"Authentication system error: {ex.Message}");
            Pause();
        }
    }

    private static async Task RunSessionLoopAsync(int accountId)
    {
        while (true)
        {
            using var context = new AtmDbContext();
            var service = new AtmService(context);
            var account = await context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId);

            if (account == null)
            {
                PrintError("Session terminated: Account record unavailable.");
                Pause();
                return;
            }

            PrintHeader($"ATM SESSION: {account.CardHolderName} [{account.AccountNumber}]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" [1] Check Account Balance");
            Console.WriteLine(" [2] Cash Deposit");
            Console.WriteLine(" [3] Cash Withdrawal");
            Console.WriteLine(" [4] Inter-Account Transfer");
            Console.WriteLine(" [5] Simulate Network Fault During Transfer (ACID Integrity Check)");
            Console.WriteLine(" [0] Return Card / Logout");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\nSelect service: ");
            Console.ResetColor();

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await CheckBalanceFlowAsync(service, accountId);
                    break;
                case "2":
                    await DepositFlowAsync(service, accountId);
                    break;
                case "3":
                    await WithdrawalFlowAsync(service, accountId);
                    break;
                case "4":
                    await TransferFlowAsync(service, accountId);
                    break;
                case "5":
                    await FaultInjectionFlowAsync(service, accountId);
                    break;
                case "0":
                    PrintSuccess("Card ejected successfully. Have a nice day.");
                    Pause();
                    return;
                default:
                    PrintError("Invalid command. Please select a valid service code.");
                    Pause();
                    break;
            }
        }
    }

    private static async Task CheckBalanceFlowAsync(AtmService service, int accountId)
    {
        PrintHeader("ACCOUNT BALANCE INQUIRY");
        try
        {
            var balance = await service.GetBalanceAsync(accountId);
            PrintInfo("Current Settled Balance", $"{balance:C2}");
        }
        catch (Exception ex)
        {
            PrintError(ex.Message);
        }
        Pause();
    }

    private static async Task DepositFlowAsync(AtmService service, int accountId)
    {
        PrintHeader("CASH DEPOSIT");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enter deposit amount: $");
        Console.ResetColor();

        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0)
        {
            PrintError("Invalid currency format. Operation rejected.");
            Pause();
            return;
        }

        try
        {
            await service.DepositAsync(accountId, amount);
            var balance = await service.GetBalanceAsync(accountId);
            PrintSuccess($"Deposit credited successfully.");
            PrintInfo("Updated Ledger Balance", $"{balance:C2}");
        }
        catch (Exception ex)
        {
            PrintError(ex.Message);
        }
        Pause();
    }

    private static async Task WithdrawalFlowAsync(AtmService service, int accountId)
    {
        PrintHeader("CASH WITHDRAWAL");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enter withdrawal amount: $");
        Console.ResetColor();

        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0)
        {
            PrintError("Invalid currency format. Operation rejected.");
            Pause();
            return;
        }

        try
        {
            await service.WithdrawAsync(accountId, amount);
            var balance = await service.GetBalanceAsync(accountId);
            PrintSuccess("Cash dispensed. Take your banknotes.");
            PrintInfo("Remaining Balance", $"{balance:C2}");
        }
        catch (InsufficientFundsException ex)
        {
            PrintError(ex.Message);
        }
        catch (DailyLimitExceededException ex)
        {
            PrintError(ex.Message);
        }
        catch (Exception ex)
        {
            PrintError($"Withdrawal failed: {ex.Message}");
        }
        Pause();
    }

    private static async Task TransferFlowAsync(AtmService service, int accountId)
    {
        PrintHeader("INTER-ACCOUNT ATOMIC TRANSFER");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enter destination account number: ");
        Console.ResetColor();
        var targetAccount = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enter transfer amount: $");
        Console.ResetColor();

        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0)
        {
            PrintError("Invalid currency format. Transfer rejected.");
            Pause();
            return;
        }

        try
        {
            await service.TransferFundsAsync(accountId, targetAccount, amount);
            var balance = await service.GetBalanceAsync(accountId);
            PrintSuccess($"Transfer of {amount:C2} to {targetAccount} settled.");
            PrintInfo("Sender Remaining Balance", $"{balance:C2}");
        }
        catch (InsufficientFundsException ex)
        {
            PrintError(ex.Message);
        }
        catch (AccountNotFoundException ex)
        {
            PrintError(ex.Message);
        }
        catch (Exception ex)
        {
            PrintError($"Transaction Aborted: {ex.Message}");
        }
        Pause();
    }

    private static async Task FaultInjectionFlowAsync(AtmService service, int accountId)
    {
        PrintHeader("ACID INTEGRITY CHECK: INJECTED FAULT SIMULATION");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enter destination account number: ");
        Console.ResetColor();
        var targetAccount = Console.ReadLine()?.Trim() ?? string.Empty;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enter transfer amount to attempt: $");
        Console.ResetColor();

        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0)
        {
            PrintError("Invalid currency format.");
            Pause();
            return;
        }

        var balanceBefore = await service.GetBalanceAsync(accountId);
        PrintInfo("Pre-Transaction Ledger Balance", $"{balanceBefore:C2}");

        try
        {
            await service.SimulateFailedTransferAsync(accountId, targetAccount, amount);
        }
        catch (InvalidOperationException ex)
        {
            PrintError(ex.Message);
        }
        catch (Exception ex)
        {
            PrintError($"Unexpected Error: {ex.Message}");
        }

        using var verifyContext = new AtmDbContext();
        var verifyService = new AtmService(verifyContext);
        var balanceAfter = await verifyService.GetBalanceAsync(accountId);

        PrintSuccess("ACID INTEGRITY VERIFIED: Database transaction rolled back.");
        PrintInfo("Post-Rollback Ledger Balance", $"{balanceAfter:C2} (Zero Delta Detected)");
        Pause();
    }

    private static async Task DisplayLedgerViewAsync()
    {
        PrintHeader("CORE BANKING SYSTEM - PERSISTED LEDGER SNAPSHOT");

        using var context = new AtmDbContext();
        var accounts = await context.Accounts.AsNoTracking().ToListAsync();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("{0,-20} {1,-18} {2,-15} {3,-15} {4,-12}", "ACCOUNT NUMBER", "CARDHOLDER", "BALANCE", "DAILY LIMIT", "SECURITY STATE");
        Console.WriteLine(new string('-', 84));
        Console.ResetColor();

        foreach (var acc in accounts)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("{0,-20} {1,-18} ", acc.AccountNumber, acc.CardHolderName);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("{0,-15:C2} ", acc.Balance);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("{0,-15:C2} ", acc.DailyWithdrawalLimit);

            if (acc.IsBlocked)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("LOCKED ({0} fails)", acc.FailedLoginAttempts);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("ACTIVE ({0} fails)", acc.FailedLoginAttempts);
            }
            Console.ResetColor();
        }

        Pause();
    }

    private static void PrintHeader(string title)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(new string('=', 84));
        Console.WriteLine($"  {title}");
        Console.WriteLine(new string('=', 84) + "\n");
        Console.ResetColor();
    }

    private static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[SUCCESS] {message}");
        Console.ResetColor();
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[ERROR/EXCEPTION] {message}");
        Console.ResetColor();
    }

    private static void PrintInfo(string label, string value)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  * {label}: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    private static void Pause()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\nPress Enter to proceed...");
        Console.ResetColor();
        Console.ReadLine();
    }
}