
using Dapper;
using TatumWelcome2.Database;
using TatumWelcome2.DTOs;

namespace TatumWelcome2.Repositories;

public sealed class IncomingTransferOutcome
{
    public string Reference { get; set; } = "";
    public string SenderAccountNumber { get; set; } = "";
    public string RecipientAccountNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}

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
            // Reserve the reference using the database's unique key.
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
                var previous =
                    connection.QuerySingleOrDefault<IncomingTransferOutcome>(
                        """
                        SELECT Reference, SenderAccountNumber,
                               RecipientAccountNumber, Amount, Status
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

                if (!sameRequest)
                    return "ReferenceConflict";

                return previous.Status switch
                {
                    "Successful" => "AlreadyProcessed",
                    "Rejected" => "Rejected",
                    _ => "Pending"
                };
            }

            var recipient =
                connection.QuerySingleOrDefault<RecipientRow>(
                    """
                    SELECT Balance
                    FROM Accounts
                    WHERE AccountNumber = @RecipientAccountNumber
                    FOR UPDATE;
                    """,
                    new { request.RecipientAccountNumber },
                    transaction);

            const decimal maxBalance = 9999999999999999.99m;

            if (recipient == null ||
                recipient.Balance > maxBalance - request.Amount)
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

                return "Rejected";
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
                    Description =
                        $"Interbank transfer from {request.SenderAccountNumber}",
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

            return "Success";
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public IncomingTransferOutcome? GetOutcome(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        using var connection = _factory.CreateConnection();

        return connection.QuerySingleOrDefault<IncomingTransferOutcome>(
            """
            SELECT Reference, SenderAccountNumber,
                   RecipientAccountNumber, Amount, Status
            FROM IncomingInterbankTransfers
            WHERE Reference = @Reference;
            """,
            new { Reference = reference });
    }

    public InterbankTransferRequestDto? FindProcessedTransfer(
        string reference)
    {
        IncomingTransferOutcome? outcome = GetOutcome(reference);

        if (outcome == null || outcome.Status != "Successful")
            return null;

        return new InterbankTransferRequestDto
        {
            Reference = outcome.Reference,
            SenderAccountNumber = outcome.SenderAccountNumber,
            RecipientAccountNumber = outcome.RecipientAccountNumber,
            Amount = outcome.Amount
        };
    }

    private sealed class RecipientRow
    {
        public decimal Balance { get; set; }
    }
}
