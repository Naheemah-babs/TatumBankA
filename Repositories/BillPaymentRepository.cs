
using Dapper;
using TatumWelcome2.Database;
using TatumWelcome2.DTOs;

namespace TatumWelcome2.Repositories;

public sealed class BillPaymentRepository
{
    private readonly DbConnectionFactory _factory;

    public BillPaymentRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public string GetPaymentMode()
    {
        using var connection = _factory.CreateConnection();

        return connection.QuerySingle<string>(
            """
            SELECT PaymentMode
            FROM BillPaymentSettings
            WHERE Id = 1;
            """);
    }

    public void SetPaymentMode(string mode)
    {
        using var connection = _factory.CreateConnection();

        connection.Execute(
            """
            UPDATE BillPaymentSettings
            SET PaymentMode = @Mode
            WHERE Id = 1;
            """,
            new { Mode = mode });
    }

    public BillPaymentResponseDto ProcessPayment(
        BillPaymentRequestDto request)
    {
        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Insert a placeholder to reserve the reference.
            // A competing request using the same reference must wait.
            int inserted = connection.Execute(
                """
                INSERT IGNORE INTO BillPayments
                    (Reference, BankId, AccountNumber,
                     PaymentType, Network, PhoneNumber,
                     Amount, DataPlanName, Success, Message)
                VALUES
                    (@Reference, @BankId, @AccountNumber,
                     @PaymentType, @Network, @PhoneNumber,
                     @Amount, @DataPlanName, FALSE, 'Processing');
                """,
                request,
                transaction);

            if (inserted == 0)
            {
                BillPaymentRow? previous =
                    connection.QuerySingleOrDefault<BillPaymentRow>(
                        """
                        SELECT *
                        FROM BillPayments
                        WHERE Reference = @Reference;
                        """,
                        new { request.Reference },
                        transaction);

                transaction.Rollback();

                if (previous == null)
                {
                    return Failed(
                        request.Reference,
                        "Unable to retrieve previous payment.");
                }

                bool sameRequest =
                    previous.BankId == request.BankId &&
                    previous.AccountNumber == request.AccountNumber &&
                    previous.PaymentType == request.PaymentType &&
                    previous.Network == request.Network &&
                    previous.PhoneNumber == request.PhoneNumber &&
                    previous.Amount == request.Amount &&
                    previous.DataPlanName == request.DataPlanName;

                if (!sameRequest)
                {
                    return Failed(
                        request.Reference,
                        "Payment reference already used for another request.");
                }

                return new BillPaymentResponseDto
                {
                    Success = previous.Success,
                    Message = previous.Message,
                    Reference = previous.Reference
                };
            }

            string mode = connection.QuerySingle<string>(
                """
                SELECT PaymentMode
                FROM BillPaymentSettings
                WHERE Id = 1;
                """,
                transaction: transaction);

            bool success = mode switch
            {
                "Success" => true,
                "Failure" => false,
                _ => Random.Shared.Next(100) < 80
            };

            string message = success
                ? "Bill payment successful."
                : "Bill payment failed. Please try again.";

            connection.Execute(
                """
                UPDATE BillPayments
                SET Success = @Success,
                    Message = @Message
                WHERE Reference = @Reference;
                """,
                new
                {
                    Success = success,
                    Message = message,
                    request.Reference
                },
                transaction);

            transaction.Commit();

            return new BillPaymentResponseDto
            {
                Success = success,
                Message = message,
                Reference = request.Reference
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static BillPaymentResponseDto Failed(
        string reference,
        string message)
    {
        return new BillPaymentResponseDto
        {
            Success = false,
            Message = message,
            Reference = reference
        };
    }

    private sealed class BillPaymentRow
    {
        public string Reference { get; set; } = "";
        public string BankId { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string PaymentType { get; set; } = "";
        public string Network { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public decimal Amount { get; set; }
        public string? DataPlanName { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }
}
