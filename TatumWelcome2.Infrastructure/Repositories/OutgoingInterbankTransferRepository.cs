using Dapper;
using Microsoft.Data.SqlClient;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;
using TatumWelcome2.Infrastructure.Database;

namespace TatumWelcome2.Infrastructure.Repositories;

public sealed class OutgoingInterbankTransferRepository : IInterbankOutgoingTransferRepository
{
    private readonly IDbConnectionFactory _factory;
    private readonly IPinHasher _pinHasher;

    public OutgoingInterbankTransferRepository(
        IDbConnectionFactory factory,
        IPinHasher pinHasher)
    {
        _factory = factory;
        _pinHasher = pinHasher;
    }

    public InterbankOutgoingTransferRecord? GetByReference(string reference)
    {
        using var connection = _factory.CreateConnection();

        return connection.QuerySingleOrDefault<InterbankOutgoingTransferRecord>(
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
        string reference,
        string description)
    {
        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            SenderRow? sender = connection.QuerySingleOrDefault<SenderRow>(
                """
                SELECT PinHash, Balance, IsFrozen
                FROM Accounts WITH (UPDLOCK, ROWLOCK)
                WHERE AccountNumber = @SenderNumber;
                """,
                new { SenderNumber = senderNumber },
                transaction);

            if (sender == null)
                return Abort(transaction, TransactionResult.AccountNotFound);

            string? existing = connection.QuerySingleOrDefault<string>(
                """
                SELECT Reference
                FROM OutgoingInterbankTransfers
                WHERE Reference = @Reference;
                """,
                new { Reference = reference },
                transaction);

            if (existing != null)
                return Abort(transaction, TransactionResult.DuplicateReference);

            if (!_pinHasher.Verify(pin, sender.PinHash))
                return Abort(transaction, TransactionResult.InvalidPin);

            if (sender.IsFrozen)
                return Abort(transaction, TransactionResult.AccountFrozen);

            if (sender.Balance < amount)
                return Abort(transaction, TransactionResult.InsufficientFunds);

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
                    Description = description,
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
        catch (SqlException ex) when (SqlServerErrors.IsUniqueViolation(ex))
        {
            transaction.Rollback();
            return TransactionResult.DuplicateReference;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<InterbankOutgoingTransferRecord> GetPendingTransfers(
        WorkPartition partition)
    {
        partition.EnsureValid();

        using var connection = _factory.CreateConnection();

        // The hash is computed by SQL Server (CHECKSUM), so every instance
        // agrees on ownership (string.GetHashCode differs per process).
        // CAST to BIGINT before ABS avoids an overflow on the smallest INT.
        return connection.Query<InterbankOutgoingTransferRecord>(
            """
            SELECT Reference, SenderAccountNumber,
                   RecipientAccountNumber, BankUrl,
                   Amount, Status
            FROM OutgoingInterbankTransfers
            WHERE Status = 'Pending'
              AND ABS(CAST(CHECKSUM(Reference) AS BIGINT)) % @PartitionCount = @PartitionIndex
            ORDER BY CreatedAt;
            """,
            new
            {
                PartitionIndex = partition.Index,
                PartitionCount = partition.Count
            }).ToList();
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

    public bool Reverse(string reference, string description)
    {
        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            InterbankOutgoingTransferRecord? transfer =
                connection.QuerySingleOrDefault<InterbankOutgoingTransferRecord>(
                    """
                    SELECT Reference, SenderAccountNumber,
                           RecipientAccountNumber, BankUrl,
                           Amount, Status
                    FROM OutgoingInterbankTransfers WITH (UPDLOCK, ROWLOCK)
                    WHERE Reference = @Reference;
                    """,
                    new { Reference = reference },
                    transaction);

            if (transfer == null ||
                transfer.Status != InterbankTransferStatus.Pending)
            {
                transaction.Rollback();
                return false;
            }

            decimal balance = connection.QuerySingle<decimal>(
                """
                SELECT Balance
                FROM Accounts WITH (UPDLOCK, ROWLOCK)
                WHERE AccountNumber = @AccountNumber;
                """,
                new { AccountNumber = transfer.SenderAccountNumber },
                transaction);

            if (balance > AccountLimits.MaximumBalance - transfer.Amount)
            {
                transaction.Rollback();
                return false;
            }

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
                    Description = description,
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

    private static TransactionResult Abort(
        SqlTransaction transaction,
        TransactionResult result)
    {
        transaction.Rollback();
        return result;
    }

    private sealed class SenderRow
    {
        public string PinHash { get; set; } = "";
        public decimal Balance { get; set; }
        public bool IsFrozen { get; set; }
    }
}