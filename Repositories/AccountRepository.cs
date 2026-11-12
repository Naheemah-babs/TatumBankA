
using Dapper;
using MySqlConnector;
using TatumWelcome2.BLL;
using TatumWelcome2.Database;
using TatumWelcome2.Interfaces;
using TatumWelcome2.Models;
using TatumWelcome2.Services;

namespace TatumWelcome2.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public AccountRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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

        Account account = new Account
        {
            AccountNumber = row.AccountNumber,
            Name = row.Name,
            Branch = row.Branch,
            Phone = row.Phone,
            Pin = row.PinHash
        };

        List<Transaction> transactions = history.Select(item =>
            new Transaction
            {
                Type = Enum.Parse<TransactionType>(item.Type),
                Amount = item.Amount,
                Date = item.Date,
                Description = item.Description,
                Reference = item.Reference
            }).ToList();

        account.LoadFromDatabase(
            row.Balance,
            row.IsFrozen,
            transactions);

        return account;
    }

    public AccountCreationResult CreateAccount(Account account)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            const string accountSql = """
                INSERT INTO Accounts
                    (AccountNumber, Name, Branch, PinHash,
                     Phone, Balance, IsFrozen)
                VALUES
                    (@AccountNumber, @Name, @Branch, @PinHash,
                     @Phone, @Balance, @IsFrozen);
                """;

            connection.Execute(
                accountSql,
                new
                {
                    account.AccountNumber,
                    account.Name,
                    account.Branch,
                    PinHash = PinHasher.Hash(account.Pin),
                    account.Phone,
                    account.Balance,
                    account.IsFrozen
                },
                transaction);

            const string historySql = """
                INSERT INTO Transactions
                    (AccountNumber, Type, Amount, Date,
                     Description, Reference)
                VALUES
                    (@AccountNumber, @Type, @Amount, @Date,
                     @Description, @Reference);
                """;

            foreach (Transaction item in account.Transactions)
            {
                connection.Execute(
                    historySql,
                    new
                    {
                        account.AccountNumber,
                        Type = item.Type.ToString(),
                        item.Amount,
                        item.Date,
                        item.Description,
                        Reference = item.Reference
                            ?? TransactionReferenceGenerator.Generate("DEP")
                    },
                    transaction);
            }

            transaction.Commit();

            return AccountCreationResult.Success;
        }
        catch (MySqlException ex)
            when (ex.Number == 1062 &&
                  ex.Message.Contains(
                      "UQ_Accounts_Phone",
                      StringComparison.OrdinalIgnoreCase))
        {
            transaction.Rollback();
            return AccountCreationResult.DuplicatePhone;
        }
        catch (MySqlException ex)
            when (ex.Number == 1062 &&
                  ex.Message.Contains(
                      "PRIMARY",
                      StringComparison.OrdinalIgnoreCase))
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


    public TransactionResult Deposit(string accountNumber, decimal amount)
    {
        if (amount <= 0)
            return TransactionResult.InvalidAmount;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            const string lookupSql = """
            SELECT Balance, IsFrozen
            FROM Accounts
            WHERE AccountNumber = @AccountNumber
            FOR UPDATE;
            """;

            var account = connection.QuerySingleOrDefault<DepositAccountRow>(
                lookupSql,
                new { AccountNumber = accountNumber },
                transaction);

            if (account == null)
            {
                transaction.Rollback();
                return TransactionResult.AccountNotFound;
            }

            if (account.IsFrozen)
            {
                transaction.Rollback();
                return TransactionResult.AccountFrozen;
            }

            if (account.Balance > 9999999999999999.99m - amount)
            {
                transaction.Rollback();
                return TransactionResult.BalanceLimitExceeded;
            }

            const string updateSql = """
            UPDATE Accounts
            SET Balance = Balance + @Amount
            WHERE AccountNumber = @AccountNumber;
            """;

            connection.Execute(
                updateSql,
                new
                {
                    AccountNumber = accountNumber,
                    Amount = amount
                },
                transaction);

            SaveTransaction(
                connection,
                transaction,
                accountNumber,
                TransactionType.Deposit,
                amount,
                "Cash deposit");

            transaction.Commit();
            return TransactionResult.Success;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private sealed class DepositAccountRow
    {
        public decimal Balance { get; set; }
        public bool IsFrozen { get; set; }
    }


    public TransactionResult Withdraw(
        string accountNumber,
        decimal amount,
        string pin)
    {
        if (amount <= 0)
            return TransactionResult.InvalidAmount;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            const string lookupSql = """
                SELECT AccountNumber, Name, Branch, Phone,
                       PinHash, Balance, IsFrozen
                FROM Accounts
                WHERE AccountNumber = @AccountNumber
                FOR UPDATE;
                """;

            AccountRow? row = connection.QuerySingleOrDefault<AccountRow>(
                lookupSql,
                new { AccountNumber = accountNumber },
                transaction);

            if (row == null)
            {
                transaction.Rollback();
                return TransactionResult.AccountNotFound;
            }

            Account account = new Account
            {
                Pin = row.PinHash
            };

            if (!AccountService.VerifyPin(account, pin))
            {
                transaction.Rollback();
                return TransactionResult.InvalidPin;
            }

            if (row.IsFrozen)
            {
                transaction.Rollback();
                return TransactionResult.AccountFrozen;
            }

            if (amount > row.Balance)
            {
                transaction.Rollback();
                return TransactionResult.InsufficientFunds;
            }

            const string updateSql = """
                UPDATE Accounts
                SET Balance = Balance - @Amount
                WHERE AccountNumber = @AccountNumber;
                """;

            connection.Execute(
                updateSql,
                new
                {
                    AccountNumber = accountNumber,
                    Amount = amount
                },
                transaction);

            SaveTransaction(
                connection,
                transaction,
                accountNumber,
                TransactionType.Withdrawal,
                amount,
                "Cash withdrawal");

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
        string pin)
    {
        if (senderNumber == recipientNumber)
            return TransactionResult.SameAccount;

        if (amount <= 0)
            return TransactionResult.InvalidAmount;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            const string lookupSql = """
                SELECT AccountNumber, Name, PinHash, Balance, IsFrozen
                FROM Accounts
                WHERE AccountNumber IN (@SenderNumber, @RecipientNumber)
                ORDER BY AccountNumber
                FOR UPDATE;
                """;

            List<AccountRow> rows = connection.Query<AccountRow>(
                lookupSql,
                new
                {
                    SenderNumber = senderNumber,
                    RecipientNumber = recipientNumber
                },
                transaction).ToList();

            AccountRow? sender = rows.FirstOrDefault(
                account => account.AccountNumber == senderNumber);

            AccountRow? recipient = rows.FirstOrDefault(
                account => account.AccountNumber == recipientNumber);

            if (sender == null)
            {
                transaction.Rollback();
                return TransactionResult.AccountNotFound;
            }

            if (recipient == null)
            {
                transaction.Rollback();
                return TransactionResult.InvalidRecipient;
            }

            Account pinAccount = new Account
            {
                Pin = sender.PinHash
            };

            if (!AccountService.VerifyPin(pinAccount, pin))
            {
                transaction.Rollback();
                return TransactionResult.InvalidPin;
            }

            if (sender.IsFrozen)
            {
                transaction.Rollback();
                return TransactionResult.AccountFrozen;
            }

            if (amount > sender.Balance)
            {
                transaction.Rollback();
                return TransactionResult.InsufficientFunds;
            }

            if (recipient.Balance > 9999999999999999.99m - amount)
            {
                transaction.Rollback();
                return TransactionResult.BalanceLimitExceeded;
            }

            string reference =
                TransactionReferenceGenerator.Generate("TRF");

            const string debitSql = """
                UPDATE Accounts
                SET Balance = Balance - @Amount
                WHERE AccountNumber = @AccountNumber;
                """;

            const string creditSql = """
                UPDATE Accounts
                SET Balance = Balance + @Amount
                WHERE AccountNumber = @AccountNumber;
                """;

            connection.Execute(
                debitSql,
                new
                {
                    AccountNumber = senderNumber,
                    Amount = amount
                },
                transaction);

            connection.Execute(
                creditSql,
                new
                {
                    AccountNumber = recipientNumber,
                    Amount = amount
                },
                transaction);

            const string historySql = """
                INSERT INTO Transactions
                    (AccountNumber, Type, Amount, Date,
                     Description, Reference)
                VALUES
                    (@AccountNumber, @Type, @Amount, @Date,
                     @Description, @Reference);
                """;

            DateTime date = DateTime.Now;

            connection.Execute(
                historySql,
                new
                {
                    AccountNumber = senderNumber,
                    Type = TransactionType.TransferOut.ToString(),
                    Amount = amount,
                    Date = date,
                    Description =
                        $"Transfer to {recipient.Name} ({recipientNumber})",
                    Reference = reference
                },
                transaction);

            connection.Execute(
                historySql,
                new
                {
                    AccountNumber = recipientNumber,
                    Type = TransactionType.TransferIn.ToString(),
                    Amount = amount,
                    Date = date,
                    Description =
                        $"Transfer from {sender.Name} ({senderNumber})",
                    Reference = reference
                },
                transaction);

            transaction.Commit();

            return TransactionResult.Success;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void SaveTransaction(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string accountNumber,
        TransactionType type,
        decimal amount,
        string description)
    {
        const string sql = """
            INSERT INTO Transactions
                (AccountNumber, Type, Amount, Date,
                 Description, Reference)
            VALUES
                (@AccountNumber, @Type, @Amount, @Date,
                 @Description, @Reference);
            """;

        string prefix = type == TransactionType.Deposit
            ? "DEP"
            : "WDR";

        connection.Execute(
            sql,
            new
            {
                AccountNumber = accountNumber,
                Type = type.ToString(),
                Amount = amount,
                Date = DateTime.Now,
                Description = description,
                Reference = TransactionReferenceGenerator.Generate(prefix)
            },
            transaction);
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


    public bool SetFrozenStatus(string accountNumber, bool freeze)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        UPDATE Accounts
        SET IsFrozen = @Freeze
        WHERE AccountNumber = @AccountNumber
          AND IsFrozen <> @Freeze;
        """;

        int updated = connection.Execute(
            sql,
            new
            {
                AccountNumber = accountNumber,
                Freeze = freeze
            });

        return updated == 1;
    }

}
