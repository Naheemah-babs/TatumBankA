using Dapper;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;
using TatumWelcome2.Infrastructure.Database;

namespace TatumWelcome2.Infrastructure.Repositories;

public sealed class IncomingInterbankTransferRepository : IIncomingInterbankTransferRepository
{
    private const string SelectTransferSql = """
        SELECT Reference, SenderAccountNumber,
               RecipientAccountNumber, Amount, Status
        FROM IncomingInterbankTransfers
        WHERE Reference = @Reference;
        """;

    private readonly IDbConnectionFactory _factory;

    public IncomingInterbankTransferRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public IncomingTransferWrite ReceiveTransfer(
        InterbankTransferRequestDto request,
        string description)
    {
        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Reserve the reference (0 rows when it already exists).
            int inserted = connection.Execute(
                """
                INSERT IGNORE INTO IncomingInterbankTransfers
                    (Reference, SenderAccountNumber,
                     RecipientAccountNumber, Amount, Status)
                VALUES
                    (@Reference, @SenderAccountNumber,
                     @RecipientAccountNumber, @Amount, 'Processing');
                """,
                request,
                transaction);

            if (inserted == 0)
            {
                IncomingTransferOutcome? existing =
                    connection.QuerySingleOrDefault<IncomingTransferOutcome>(
                        SelectTransferSql,
                        new { request.Reference },
                        transaction);

                transaction.Rollback();

                return new IncomingTransferWrite(
                    IncomingTransferWriteStatus.AlreadyExists,
                    existing);
            }

            RecipientRow? recipient = connection.QuerySingleOrDefault<RecipientRow>(
                """
                SELECT Balance
                FROM Accounts
                WHERE AccountNumber = @RecipientAccountNumber
                FOR UPDATE;
                """,
                new { request.RecipientAccountNumber },
                transaction);

            if (recipient == null ||
                recipient.Balance > AccountLimits.MaximumBalance - request.Amount)
            {
                connection.Execute(
                    """
                    UPDATE IncomingInterbankTransfers
                    SET Status = 'Rejected'
                    WHERE Reference = @Reference;
                    """,
                    new { request.Reference },
                    transaction);

                transaction.Commit();

                return new IncomingTransferWrite(
                    IncomingTransferWriteStatus.RecipientRejected);
            }

            connection.Execute(
                """
                UPDATE Accounts
                SET Balance = Balance + @Amount
                WHERE AccountNumber = @RecipientAccountNumber;
                """,
                request,
                transaction);

            connection.Execute(
                """
                INSERT INTO Transactions
                    (AccountNumber, Type, Amount, Date,
                     Description, Reference)
                VALUES
                    (@RecipientAccountNumber, 'TransferIn',
                     @Amount, @Date, @Description, @Reference);
                """,
                new
                {
                    request.RecipientAccountNumber,
                    request.Amount,
                    Date = DateTime.UtcNow,
                    Description = description,
                    request.Reference
                },
                transaction);

            connection.Execute(
                """
                UPDATE IncomingInterbankTransfers
                SET Status = 'Successful'
                WHERE Reference = @Reference;
                """,
                new { request.Reference },
                transaction);

            transaction.Commit();

            return new IncomingTransferWrite(IncomingTransferWriteStatus.Credited);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public IncomingTransferOutcome? GetOutcome(string reference)
    {
        using var connection = _factory.CreateConnection();

        return connection.QuerySingleOrDefault<IncomingTransferOutcome>(
            SelectTransferSql,
            new { Reference = reference });
    }

    private sealed class RecipientRow
    {
        public decimal Balance { get; set; }
    }
}
