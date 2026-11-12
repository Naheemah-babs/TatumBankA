
using Dapper;
using MySqlConnector;
using TatumWelcome2.BLL;
using TatumWelcome2.Database;
using TatumWelcome2.Models;

namespace TatumWelcome2.Repositories;

public sealed class OutgoingInterbankTransferRecord
{
    public string Reference { get; set; } = "";
    public string SenderAccountNumber { get; set; } = "";
    public string RecipientAccountNumber { get; set; } = "";
    public string BankUrl { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}

public sealed class OutgoingInterbankTransferRepository
{
    private readonly DbConnectionFactory _factory;

    public OutgoingInterbankTransferRepository(
        DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public OutgoingInterbankTransferRecord? GetByReference(
        string reference)
    {
        using var connection = _factory.CreateConnection();

        return connection.QuerySingleOrDefault
            <OutgoingInterbankTransferRecord>(
            """
            SELECT Reference, SenderAccountNumber,
                   RecipientAccountNumber, BankUrl,
                   Amount, Status
            FROM OutgoingInterbankTransfers
            WHERE Reference = @Reference;
            """,
            new { Reference = reference });
    }

    public TransactionResult DebitAndCreate(
        string senderNumber,
        string recipientNumber,
        string bankUrl,
        decimal amount,
        string pin,
        string reference)
    {
        if (amount <= 0)
            return TransactionResult.InvalidAmount;

        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var sender = connection.QuerySingleOrDefault<SenderRow>(
                """
                SELECT PinHash, Balance, IsFrozen
                FROM Accounts
                WHERE AccountNumber = @SenderNumber
                FOR UPDATE;
                """,
                new { SenderNumber = senderNumber },
                transaction);

            if (sender == null)
                return TransactionResult.AccountNotFound;

            var existing = connection.QuerySingleOrDefault<string>(
                """
                SELECT Reference
                FROM OutgoingInterbankTransfers
                WHERE Reference = @Reference;
                """,
                new { Reference = reference },
                transaction);

            if (existing != null)
                return TransactionResult.InvalidAmount;

            var account = new Account { Pin = sender.PinHash };

            if (!AccountService.VerifyPin(account, pin))
                return TransactionResult.InvalidPin;

            if (sender.IsFrozen)
                return TransactionResult.AccountFrozen;

            if (sender.Balance < amount)
                return TransactionResult.InsufficientFunds;

            connection.Execute(
                """
                UPDATE Accounts
                SET Balance = Balance - @Amount
                WHERE AccountNumber = @SenderNumber;
                """,
                new { SenderNumber = senderNumber, Amount = amount },
                transaction);

            connection.Execute(
                """
                INSERT INTO Transactions
                    (AccountNumber, Type, Amount, Date,
                     Description, Reference)
                VALUES
                    (@SenderNumber, 'TransferOut', @Amount,
                     @Date, @Description, @Reference);
                """,
                new
                {
                    SenderNumber = senderNumber,
                    Amount = amount,
                    Date = DateTime.UtcNow,
                    Description =
                        $"Interbank transfer to {recipientNumber}",
                    Reference = reference
                },
                transaction);

            connection.Execute(
                """
                INSERT INTO OutgoingInterbankTransfers
                    (Reference, SenderAccountNumber,
                     RecipientAccountNumber, BankUrl,
                     Amount, Status)
                VALUES
                    (@Reference, @SenderNumber,
                     @RecipientNumber, @BankUrl,
                     @Amount, 'Pending');
                """,
                new
                {
                    Reference = reference,
                    SenderNumber = senderNumber,
                    RecipientNumber = recipientNumber,
                    BankUrl = bankUrl,
                    Amount = amount
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

    public List<OutgoingInterbankTransferRecord>
        GetPendingTransfers()
    {
        using var connection = _factory.CreateConnection();

        return connection.Query<OutgoingInterbankTransferRecord>(
            """
            SELECT Reference, SenderAccountNumber,
                   RecipientAccountNumber, BankUrl,
                   Amount, Status
            FROM OutgoingInterbankTransfers
            WHERE Status = 'Pending'
            ORDER BY CreatedAt;
            """).ToList();
    }

    public void MarkSuccessful(string reference)
    {
        using var connection = _factory.CreateConnection();

        connection.Execute(
            """
            UPDATE OutgoingInterbankTransfers
            SET Status = 'Successful'
            WHERE Reference = @Reference
              AND Status = 'Pending';
            """,
            new { Reference = reference });
    }

    public bool Reverse(string reference)
    {
        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var transfer =
                connection.QuerySingleOrDefault
                    <OutgoingInterbankTransferRecord>(
                    """
                    SELECT Reference, SenderAccountNumber,
                           RecipientAccountNumber, BankUrl,
                           Amount, Status
                    FROM OutgoingInterbankTransfers
                    WHERE Reference = @Reference
                    FOR UPDATE;
                    """,
                    new { Reference = reference },
                    transaction);

            if (transfer == null ||
                transfer.Status != "Pending")
            {
                return false;
            }

            var balance = connection.QuerySingle<decimal>(
                """
                SELECT Balance
                FROM Accounts
                WHERE AccountNumber = @AccountNumber
                FOR UPDATE;
                """,
                new
                {
                    AccountNumber = transfer.SenderAccountNumber
                },
                transaction);

            const decimal maxBalance = 9999999999999999.99m;

            if (balance > maxBalance - transfer.Amount)
                return false;

            connection.Execute(
                """
                UPDATE Accounts
                SET Balance = Balance + @Amount
                WHERE AccountNumber = @AccountNumber;
                """,
                new
                {
                    AccountNumber = transfer.SenderAccountNumber,
                    transfer.Amount
                },
                transaction);

            connection.Execute(
                """
                INSERT INTO Transactions
                    (AccountNumber, Type, Amount, Date,
                     Description, Reference)
                VALUES
                    (@AccountNumber, 'TransferIn', @Amount,
                     @Date, @Description, @Reference);
                """,
                new
                {
                    AccountNumber = transfer.SenderAccountNumber,
                    transfer.Amount,
                    Date = DateTime.UtcNow,
                    Description =
                        $"Reversal of rejected interbank transfer {reference}",
                    Reference = reference
                },
                transaction);

            connection.Execute(
                """
                UPDATE OutgoingInterbankTransfers
                SET Status = 'Reversed'
                WHERE Reference = @Reference;
                """,
                new { Reference = reference },
                transaction);

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private sealed class SenderRow
    {
        public string PinHash { get; set; } = "";
        public decimal Balance { get; set; }
        public bool IsFrozen { get; set; }
    }
}
