using Dapper;
using Microsoft.Data.SqlClient;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.BLL.Services;
using TatumWelcome2.Domain;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;
using TatumWelcome2.Infrastructure.Database;

namespace TatumWelcome2.Infrastructure.Repositories;

/// <summary>
/// /// Accounts and their ledger. Money-moving methods lock the affected rows
/// (UPDLOCK) and apply only the checks that must hold under that lock: PIN
/// </summary>
public sealed class AccountRepository : IAccountRepository
{
    private const string InsertTransactionSql = """
        INSERT INTO Transactions
            (AccountNumber, Type, Amount, Date,
             Description, Reference)
        VALUES
            (@AccountNumber, @Type, @Amount, @Date,
             @Description, @Reference);
        """;

    private const string CreditSql = """
        UPDATE Accounts
        SET Balance = Balance + @Amount
        WHERE AccountNumber = @AccountNumber;
        """;

    private const string DebitSql = """
        UPDATE Accounts
        SET Balance = Balance - @Amount
        WHERE AccountNumber = @AccountNumber;
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPinHasher _pinHasher;

    public AccountRepository(
        IDbConnectionFactory connectionFactory,
        IPinHasher pinHasher)
    {
        _connectionFactory = connectionFactory;
        _pinHasher = pinHasher;
    }

    public Account? FindByAccountNumber(string accountNumber)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string accountSql = """
            SELECT AccountNumber, Name, Branch, Phone,
                   PinHash, Balance, IsFrozen
            FROM Accounts
            WHERE AccountNumber = @AccountNumber;
            """;

        var row = connection.QuerySingleOrDefault<AccountRow>(
            accountSql,
            new { AccountNumber = accountNumber });

        if (row == null)
            return null;

        const string historySql = """
            SELECT Type, Amount, Date, Description, Reference
            FROM Transactions
            WHERE AccountNumber = @AccountNumber
            ORDER BY TransactionId;
            """;

        var history = connection.Query<TransactionRow>(
            historySql,
            new { AccountNumber = accountNumber });

        List<Transaction> transactions = history.Select(item =>
            new Transaction
            {
                Type = Enum.Parse<TransactionType>(item.Type),
                Amount = item.Amount,
                Date = item.Date,
                Description = item.Description,
                Reference = item.Reference
            }).ToList();

        return new Account
        {
            AccountNumber = row.AccountNumber,
            Name = row.Name,
            Branch = row.Branch,
            Phone = row.Phone,
            PinHash = row.PinHash,
            Balance = row.Balance,
            IsFrozen = row.IsFrozen,
            Transactions = transactions
        };
    }

    public AccountCreationResult CreateAccount(NewAccount account)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            connection.Execute(
                """
                INSERT INTO Accounts
                    (AccountNumber, Name, Branch, PinHash,
                     Phone, Balance, IsFrozen)
                VALUES
                    (@AccountNumber, @Name, @Branch, @PinHash,
                     @Phone, @Balance, @IsFrozen);
                """,
                new
                {
                    account.AccountNumber,
                    account.Name,
                    account.Branch,
                    account.PinHash,
                    account.Phone,
                    Balance = account.OpeningDeposit,
                    IsFrozen = false
                },
                transaction);

            InsertTransaction(
                connection,
                transaction,
                account.AccountNumber,
                TransactionType.Deposit,
                account.OpeningDeposit,
                LocalClock.Now,
                account.OpeningDepositDescription,
                account.OpeningDepositReference);

            transaction.Commit();

            return AccountCreationResult.Success;
        }
        catch (SqlException ex)
            when (SqlServerErrors.IsUniqueViolation(ex, "UQ_Accounts_Phone"))
        {
            transaction.Rollback();
            return AccountCreationResult.DuplicatePhone;
        }
        catch (SqlException ex)
            when (SqlServerErrors.IsUniqueViolation(ex, "PK_Accounts"))
        {
            transaction.Rollback();
            return AccountCreationResult.DuplicateAccountNumber;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public TransactionResult Deposit(
        string accountNumber,
        decimal amount,
        string reference,
        string description)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            LockedAccountRow? account = LockAccount(connection, transaction, accountNumber);

            if (account == null)
                return Abort(transaction, TransactionResult.AccountNotFound);

            if (account.IsFrozen)
                return Abort(transaction, TransactionResult.AccountFrozen);

            if (account.Balance > AccountLimits.MaximumBalance - amount)
                return Abort(transaction, TransactionResult.BalanceLimitExceeded);

            connection.Execute(
                CreditSql,
                new { AccountNumber = accountNumber, Amount = amount },
                transaction);

            InsertTransaction(
                connection,
                transaction,
                accountNumber,
                TransactionType.Deposit,
                amount,
                LocalClock.Now,
                description,
                reference);

            transaction.Commit();
            return TransactionResult.Success;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public TransactionResult Withdraw(
        string accountNumber,
        decimal amount,
        string pin,
        string reference,
        string description)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            LockedAccountRow? account = LockAccount(connection, transaction, accountNumber);

            if (account == null)
                return Abort(transaction, TransactionResult.AccountNotFound);

            if (!_pinHasher.Verify(pin, account.PinHash))
                return Abort(transaction, TransactionResult.InvalidPin);

            if (account.IsFrozen)
                return Abort(transaction, TransactionResult.AccountFrozen);

            if (amount > account.Balance)
                return Abort(transaction, TransactionResult.InsufficientFunds);

            connection.Execute(
                DebitSql,
                new { AccountNumber = accountNumber, Amount = amount },
                transaction);

            InsertTransaction(
                connection,
                transaction,
                accountNumber,
                TransactionType.Withdrawal,
                amount,
                LocalClock.Now,
                description,
                reference);

            transaction.Commit();
            return TransactionResult.Success;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public TransactionResult Transfer(
        string senderNumber,
        string recipientNumber,
        decimal amount,
        string pin,
        string reference)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {

            // Always lock the lower account number first, so opposite transfers
            // (A->B and B->A) queue up on the same row instead of deadlocking.
            bool senderFirst = string.CompareOrdinal(senderNumber, recipientNumber) <= 0;
            string firstNumber = senderFirst ? senderNumber : recipientNumber;
            string secondNumber = senderFirst ? recipientNumber : senderNumber;

            LockedAccountRow? first = LockAccount(connection, transaction, firstNumber);
            LockedAccountRow? second = LockAccount(connection, transaction, secondNumber);

            LockedAccountRow? sender = senderFirst ? first : second;
            LockedAccountRow? recipient = senderFirst ? second : first;
            if (sender == null)
                return Abort(transaction, TransactionResult.AccountNotFound);

            if (recipient == null)
                return Abort(transaction, TransactionResult.InvalidRecipient);

            if (!_pinHasher.Verify(pin, sender.PinHash))
                return Abort(transaction, TransactionResult.InvalidPin);

            if (sender.IsFrozen)
                return Abort(transaction, TransactionResult.AccountFrozen);

            if (amount > sender.Balance)
                return Abort(transaction, TransactionResult.InsufficientFunds);

            if (recipient.Balance > AccountLimits.MaximumBalance - amount)
                return Abort(transaction, TransactionResult.BalanceLimitExceeded);

            connection.Execute(
                DebitSql,
                new { AccountNumber = senderNumber, Amount = amount },
                transaction);

            connection.Execute(
                CreditSql,
                new { AccountNumber = recipientNumber, Amount = amount },
                transaction);

            DateTime date = LocalClock.Now;

            // Names come from the locked rows, so the wording is applied here.
            InsertTransaction(
                connection,
                transaction,
                senderNumber,
                TransactionType.TransferOut,
                amount,
                date,
                TransactionDescriptions.TransferTo(recipient.Name, recipientNumber),
                reference);

            InsertTransaction(
                connection,
                transaction,
                recipientNumber,
                TransactionType.TransferIn,
                amount,
                date,
                TransactionDescriptions.TransferFrom(sender.Name, senderNumber),
                reference);

            transaction.Commit();
            return TransactionResult.Success;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public bool SetFrozenStatus(string accountNumber, bool freeze)
    {
        using var connection = _connectionFactory.CreateConnection();

        int updated = connection.Execute(
            """
            UPDATE Accounts
            SET IsFrozen = @Freeze
            WHERE AccountNumber = @AccountNumber
              AND IsFrozen <> @Freeze;
            """,
            new
            {
                AccountNumber = accountNumber,
                Freeze = freeze
            });

        return updated == 1;
    }

    public IReadOnlyList<LegacyPin> GetLegacyPins()
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.Query<LegacyPin>(
            """
            SELECT AccountNumber, PinHash AS StoredValue
            FROM Accounts
            WHERE PinHash NOT LIKE 'PBKDF2$%';
            """).ToList();
    }

    public bool ReplacePinHash(
        string accountNumber,
        string currentValue,
        string newHash)
    {
        using var connection = _connectionFactory.CreateConnection();

        int updated = connection.Execute(
            """
            UPDATE Accounts
            SET PinHash = @NewHash
            WHERE AccountNumber = @AccountNumber
              AND PinHash = @CurrentValue;
            """,
            new
            {
                AccountNumber = accountNumber,
                CurrentValue = currentValue,
                NewHash = newHash
            });

        return updated == 1;
    }
    public PinAttemptOutcome VerifyPinWithLockout(string accountNumber, string pin)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            // The row lock makes parallel guesses queue up, so none can dodge the counter.
            PinStateRow? row = connection.QuerySingleOrDefault<PinStateRow>(
                """
                SELECT PinHash,
                       CASE WHEN LockedUntil IS NOT NULL
                                 AND LockedUntil > SYSUTCDATETIME()
                            THEN DATEDIFF(SECOND, SYSUTCDATETIME(), LockedUntil)
                            ELSE 0 END AS LockSecondsRemaining
                FROM Accounts WITH (UPDLOCK, ROWLOCK)
                WHERE AccountNumber = @AccountNumber;
                """,
                new { AccountNumber = accountNumber },
                transaction);

            if (row == null)
            {
                transaction.Rollback();
                return PinAttemptOutcome.AccountNotFound;
            }

            // Locked: refuse without checking the PIN and without counting the attempt.
            if (row.LockSecondsRemaining > 0)
            {
                transaction.Rollback();
                return PinAttemptOutcome.Locked;
            }

            if (_pinHasher.Verify(pin, row.PinHash))
            {
                connection.Execute(
                    """
                    UPDATE Accounts
                    SET FailedPinAttempts = 0,
                        LastFailedPinAt = NULL,
                        LockedUntil = NULL
                    WHERE AccountNumber = @AccountNumber
                      AND (FailedPinAttempts <> 0 OR LockedUntil IS NOT NULL);
                    """,
                    new { AccountNumber = accountNumber },
                    transaction);

                transaction.Commit();
                return PinAttemptOutcome.Verified;
            }

            RecordFailedPin(connection, transaction, accountNumber);

            // Commit, not roll back: the failure count must be kept.
            transaction.Commit();
            return PinAttemptOutcome.WrongPin;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public int GetLockSecondsRemaining(string accountNumber)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.QuerySingleOrDefault<int>(
            """
            SELECT CASE WHEN LockedUntil IS NOT NULL
                             AND LockedUntil > SYSUTCDATETIME()
                        THEN DATEDIFF(SECOND, SYSUTCDATETIME(), LockedUntil)
                        ELSE 0 END
            FROM Accounts
            WHERE AccountNumber = @AccountNumber;
            """,
            new { AccountNumber = accountNumber });
    }

    /// <summary>Counts a wrong PIN (restarting after 24 quiet hours) and locks on every 5th.</summary>
    private static void RecordFailedPin(
        SqlConnection connection,
        SqlTransaction transaction,
        string accountNumber)
    {
        // SQL Server evaluates every SET expression against the old row, so FailedPinAttempts sees the old LastFailedPinAt.
        connection.Execute(
            """
            UPDATE Accounts
            SET FailedPinAttempts =
                    IIF(LastFailedPinAt IS NULL
                        OR LastFailedPinAt < DATEADD(HOUR, -@ResetHours, SYSUTCDATETIME()),
                        1,
                        FailedPinAttempts + 1),
                LastFailedPinAt = SYSUTCDATETIME()
            WHERE AccountNumber = @AccountNumber;
            """,
            new
            {
                AccountNumber = accountNumber,
                ResetHours = PinLockoutPolicy.ResetAfterHours
            },
            transaction);

        int attempts = connection.QuerySingle<int>(
            """
            SELECT FailedPinAttempts
            FROM Accounts
            WHERE AccountNumber = @AccountNumber;
            """,
            new { AccountNumber = accountNumber },
            transaction);

        int? lockSeconds = PinLockoutPolicy.LockSeconds(attempts);

        if (lockSeconds is null)
            return;

        connection.Execute(
            """
            UPDATE Accounts
            SET LockedUntil = DATEADD(SECOND, @Seconds, SYSUTCDATETIME())
            WHERE AccountNumber = @AccountNumber;
            """,
            new { AccountNumber = accountNumber, Seconds = lockSeconds.Value },
            transaction);
    }
    private static LockedAccountRow? LockAccount(
        SqlConnection connection,
        SqlTransaction transaction,
        string accountNumber)
    {
        return connection.QuerySingleOrDefault<LockedAccountRow>(
            """
            SELECT AccountNumber, Name, PinHash, Balance, IsFrozen
            FROM Accounts WITH (UPDLOCK, ROWLOCK)
            WHERE AccountNumber = @AccountNumber;
            """,
            new { AccountNumber = accountNumber },
            transaction);
    }

    private static void InsertTransaction(
        SqlConnection connection,
        SqlTransaction transaction,
        string accountNumber,
        TransactionType type,
        decimal amount,
        DateTime date,
        string description,
        string reference)
    {
        connection.Execute(
            InsertTransactionSql,
            new
            {
                AccountNumber = accountNumber,
                Type = type.ToString(),
                Amount = amount,
                Date = date,
                Description = description,
                Reference = reference
            },
            transaction);
    }

    private static TransactionResult Abort(
        SqlTransaction transaction,
        TransactionResult result)
    {
        transaction.Rollback();
        return result;
    }

    private sealed class LockedAccountRow
    {
        public string AccountNumber { get; set; } = "";
        public string Name { get; set; } = "";
        public string PinHash { get; set; } = "";
        public decimal Balance { get; set; }
        public bool IsFrozen { get; set; }
    }

    private sealed class AccountRow
    {
        public string AccountNumber { get; set; } = "";
        public string Name { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Phone { get; set; } = "";
        public string PinHash { get; set; } = "";
        public decimal Balance { get; set; }
        public bool IsFrozen { get; set; }
    }

    private sealed class TransactionRow
    {
        public string Type { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; } = "";
        public string? Reference { get; set; }
    }
    private sealed class PinStateRow
    {
        public string PinHash { get; set; } = "";
        public long LockSecondsRemaining { get; set; }
    }
}
