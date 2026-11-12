
using Dapper;
using TatumWelcome2.Database;
using TatumWelcome2.DTOs;

namespace TatumWelcome2.Repositories;

public class IncomingInterbankTransferRepository
{
    private readonly DbConnectionFactory _factory;

    public IncomingInterbankTransferRepository(
        DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public string ReceiveTransfer(
        InterbankTransferRequestDto request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Reference) ||
            request.Reference.Length > 100 ||
            string.IsNullOrWhiteSpace(request.SenderAccountNumber) ||
            request.SenderAccountNumber.Length > 10 ||
            string.IsNullOrWhiteSpace(request.RecipientAccountNumber) ||
            request.RecipientAccountNumber.Length > 10 ||
            request.Amount <= 0 ||
            request.Amount > 9999999999999999.99m ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            return "InvalidRequest";
        }

        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Lock this reference using its unique database key.
            // If another request is inserting the same reference,
            // MySQL waits until that transaction finishes.
            int inserted = connection.Execute(
                """
                INSERT IGNORE INTO IncomingInterbankTransfers
                    (Reference, SenderAccountNumber,
                     RecipientAccountNumber, Amount)
                VALUES
                    (@Reference, @SenderAccountNumber,
                     @RecipientAccountNumber, @Amount);
                """,
                request,
                transaction);

            if (inserted == 0)
            {
                var previous =
                    connection.QuerySingleOrDefault
                        <InterbankTransferRequestDto>(
                        """
                        SELECT Reference, SenderAccountNumber,
                               RecipientAccountNumber, Amount
                        FROM IncomingInterbankTransfers
                        WHERE Reference = @Reference;
                        """,
                        new { request.Reference },
                        transaction);

                transaction.Rollback();

                if (previous == null)
                    return "InvalidRequest";

                bool sameRequest =
                    previous.SenderAccountNumber ==
                        request.SenderAccountNumber &&
                    previous.RecipientAccountNumber ==
                        request.RecipientAccountNumber &&
                    previous.Amount == request.Amount;

                return sameRequest
                    ? "AlreadyProcessed"
                    : "ReferenceConflict";
            }

            // Lock recipient's account balance.
            var recipient = connection.QuerySingleOrDefault
                <RecipientRow>(
                """
                SELECT Balance
                FROM Accounts
                WHERE AccountNumber = @RecipientAccountNumber
                FOR UPDATE;
                """,
                new { request.RecipientAccountNumber },
                transaction);

            if (recipient == null)
            {
                transaction.Rollback();
                return "AccountNotFound";
            }

            const decimal maxBalance = 9999999999999999.99m;

            if (recipient.Balance > maxBalance - request.Amount)
            {
                transaction.Rollback();
                return "InvalidRequest";
            }

            // Credit recipient's MySQL account.
            connection.Execute(
                """
                UPDATE Accounts
                SET Balance = Balance + @Amount
                WHERE AccountNumber = @RecipientAccountNumber;
                """,
                request,
                transaction);

            // Save transaction history.
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
                    Description =
                        $"Interbank transfer from {request.SenderAccountNumber}",
                    request.Reference
                },
                transaction);

            transaction.Commit();
            return "Success";
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public InterbankTransferRequestDto? FindProcessedTransfer(
        string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        using var connection = _factory.CreateConnection();

        return connection.QuerySingleOrDefault
            <InterbankTransferRequestDto>(
            """
            SELECT Reference, SenderAccountNumber,
                   RecipientAccountNumber, Amount
            FROM IncomingInterbankTransfers
            WHERE Reference = @Reference;
            """,
            new { Reference = reference });
    }

    private sealed class RecipientRow
    {
        public decimal Balance { get; set; }
    }
}
