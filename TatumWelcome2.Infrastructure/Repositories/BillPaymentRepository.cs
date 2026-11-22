using Dapper;
using Microsoft.Data.SqlClient;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;
using TatumWelcome2.Infrastructure.Database;

namespace TatumWelcome2.Infrastructure.Repositories;

public sealed class BillPaymentRepository : IBillPaymentRepository
{
    // Relies on the Reference key being created with IGNORE_DUP_KEY = ON:
    // a duplicate reference inserts nothing and returns 0 rows.
    private const string ReserveReferenceSql = """
        INSERT INTO BillPayments
            (Reference, BankId, AccountNumber,
             PaymentType, Network, PhoneNumber,
             Amount, DataPlanName, Success, Message)
        VALUES
            (@Reference, @BankId, @AccountNumber,
             @PaymentType, @Network, @PhoneNumber,
             @Amount, @DataPlanName, 0, 'Processing');
        """;

    private const string SelectPaymentSql = """
        SELECT Reference, BankId, AccountNumber,
               PaymentType, Network, PhoneNumber,
               Amount, DataPlanName, Success, Message
        FROM BillPayments
        WHERE Reference = @Reference;
        """;

    private const string RecordOutcomeSql = """
        UPDATE BillPayments
        SET Success = @Success,
            Message = @Message
        WHERE Reference = @Reference;
        """;

    private readonly IDbConnectionFactory _factory;
    private readonly IPinHasher _pinHasher;

    public BillPaymentRepository(
        IDbConnectionFactory factory,
        IPinHasher pinHasher)
    {
        _factory = factory;
        _pinHasher = pinHasher;
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

    public BillPaymentWrite ProcessPayment(
        BillPaymentRequest request,
        BillProviderDecision decision)
    {
        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            // A competing request with the same reference waits on the row lock.
            int reserved = connection.Execute(
                ReserveReferenceSql,
                request,
                transaction);

            if (reserved == 0)
            {
                BillPaymentRecord? existing = FindPayment(connection, transaction, request.Reference);
                transaction.Rollback();

                return new BillPaymentWrite(BillPaymentWriteStatus.AlreadyExists, existing);
            }

            RecordOutcome(connection, transaction, request.Reference, decision);

            transaction.Commit();
            return new BillPaymentWrite(BillPaymentWriteStatus.Recorded);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public CustomerPurchaseWrite PurchaseForCustomer(
        BillPaymentRequest request,
        string pin,
        TransactionType transactionType,
        string description,
        BillProviderDecision decision)
    {
        using var connection = _factory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            CustomerAccountRow? account =
    connection.QuerySingleOrDefault<CustomerAccountRow>(
        """
        SELECT PinHash, Balance, IsFrozen
        FROM Accounts WITH (UPDLOCK, ROWLOCK)
        WHERE AccountNumber = @AccountNumber;
        """,
        new { request.AccountNumber },
        transaction);

            if (account == null)
                return Reject(transaction, TransactionResult.AccountNotFound);

            if (!_pinHasher.Verify(pin, account.PinHash))
                return Reject(transaction, TransactionResult.InvalidPin);

            int reserved = connection.Execute(
                ReserveReferenceSql,
                request,
                transaction);

            if (reserved == 0)
            {
                BillPaymentRecord? existing = FindPayment(connection, transaction, request.Reference);
                transaction.Rollback();

                return new CustomerPurchaseWrite(
                    CustomerPurchaseWriteStatus.AlreadyExists,
                    Existing: existing);
            }

            // Rolling back also releases the reservation, so the caller may retry.
            if (account.IsFrozen)
                return Reject(transaction, TransactionResult.AccountFrozen);

            if (account.Balance < request.Amount)
                return Reject(transaction, TransactionResult.InsufficientFunds);

            if (decision.Approved)
            {
                connection.Execute(
                    """
                    UPDATE Accounts
                    SET Balance = Balance - @Amount
                    WHERE AccountNumber = @AccountNumber;
                    """,
                    new { request.Amount, request.AccountNumber },
                    transaction);

                connection.Execute(
                    """
                    INSERT INTO Transactions
                        (AccountNumber, Type, Amount, Date,
                         Description, Reference)
                    VALUES
                        (@AccountNumber, @Type, @Amount, @Date,
                         @Description, @Reference);
                    """,
                    new
                    {
                        request.AccountNumber,
                        Type = transactionType.ToString(),
                        request.Amount,
                        Date = LocalClock.Now,
                        Description = description,
                        request.Reference
                    },
                    transaction);
            }

            RecordOutcome(connection, transaction, request.Reference, decision);

            transaction.Commit();
            return new CustomerPurchaseWrite(CustomerPurchaseWriteStatus.Completed);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static BillPaymentRecord? FindPayment(
        SqlConnection connection,
        SqlTransaction transaction,
        string reference)
    {
        return connection.QuerySingleOrDefault<BillPaymentRecord>(
            SelectPaymentSql,
            new { Reference = reference },
            transaction);
    }

    private static void RecordOutcome(
        SqlConnection connection,
        SqlTransaction transaction,
        string reference,
        BillProviderDecision decision)
    {
        connection.Execute(
            RecordOutcomeSql,
            new
            {
                Success = decision.Approved,
                decision.Message,
                Reference = reference
            },
            transaction);
    }

    private static CustomerPurchaseWrite Reject(
        SqlTransaction transaction,
        TransactionResult reason)
    {
        transaction.Rollback();
        return new CustomerPurchaseWrite(CustomerPurchaseWriteStatus.Rejected, reason);
    }

    private sealed class CustomerAccountRow
    {
        public string PinHash { get; set; } = "";
        public decimal Balance { get; set; }
        public bool IsFrozen { get; set; }
    }
}
