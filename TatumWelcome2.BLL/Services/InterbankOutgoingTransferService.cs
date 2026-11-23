using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.BLL.Options;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

public sealed class InterbankOutgoingTransferService : IInterbankOutgoingTransferService
{
    private readonly IAccountRepository _accounts;
    private readonly IBankApiClient _bankApiClient;
    private readonly IInterbankOutgoingTransferRepository _repository;
    private readonly ILogger<InterbankOutgoingTransferService> _logger;
    private readonly InterBankOptions _interbankOptions;

    public InterbankOutgoingTransferService(
        IBankApiClient bankApiClient,
        IInterbankOutgoingTransferRepository repository,
        IAccountRepository accounts,
        ILogger<InterbankOutgoingTransferService> logger,
        IOptions<InterBankOptions> interbankOptions)
    {
        _bankApiClient = bankApiClient;
        _repository = repository;
        _accounts = accounts;
        _logger = logger;
        _interbankOptions = interbankOptions.Value;
    }

    public Task<InterbankSendResult> SendTransferAsync(
    string senderNumber,
    SendInterbankTransferRequestDto request)
    {
        string reference = request.Reference?.Trim() ?? string.Empty;

        if (!Guid.TryParseExact(reference, "N", out _))
            return Task.FromResult(InvalidRequest(reference, "A valid transfer reference is required."));

        if (string.IsNullOrWhiteSpace(request.Pin))
            return Task.FromResult(InvalidRequest(reference, "PIN is required."));

        if (string.IsNullOrWhiteSpace(request.RecipientAccountNumber))
            return Task.FromResult(InvalidRequest(reference, "Recipient account number is required."));

        // Trim once so "123 " is validated, stored and compared as "123".
        string recipient = request.RecipientAccountNumber.Trim();

        if (recipient.Length != AccountLimits.MaximumAccountNumberLength)
        {
            return Task.FromResult(InvalidRequest(
                reference,
                $"Recipient account number must be exactly {AccountLimits.MaximumAccountNumberLength} characters."));
        }
        if (!recipient.All(char.IsAsciiDigit))
            return Task.FromResult(InvalidRequest(reference, "Recipient account number must contain digits only."));

        // Same amount rules as an intrabank transfer: whole number, at least 500.
        if (AccountLimits.CheckAmount(request.Amount) is { } amountError)
        {
            string message = amountError == TransactionResult.AmountNotWholeNumber
                ? AccountLimits.NotWholeNumberMessage
                : AccountLimits.OutOfRangeMessage;

            return Task.FromResult(InvalidRequest(reference, message));
        }

        // 1000.00 passes the check above; store and send it as 1000.
        decimal amount = decimal.Truncate(request.Amount);

        if (string.IsNullOrWhiteSpace(_interbankOptions.Url))
        {
            throw new InvalidOperationException(
                $"{InterBankOptions.SectionName}:Url is not configured.");
        }

        return SendTransferAsync(
            senderNumber,
            _interbankOptions.Url,
            recipient,
            amount,
            request.Pin,
            reference);
    }

    private static InterbankSendResult InvalidRequest(string reference, string message)
    {
        return new InterbankSendResult(
            InterbankSendOutcome.InvalidRequest,
            reference,
            message);
    }

    private async Task<InterbankSendResult> SendTransferAsync(
        string senderNumber,
        string bankUrl,
        string recipientAccountNumber,
        decimal amount,
        string pin,
        string? reference = null)
    {
        if (string.IsNullOrWhiteSpace(bankUrl) ||
            string.IsNullOrWhiteSpace(recipientAccountNumber))
        {
            return new InterbankSendResult(
                InterbankSendOutcome.InvalidRequest,
                reference ?? "",
                "Invalid bank or recipient.");
        }

        if (amount <= 0 ||
            amount > AccountLimits.MaximumBalance ||
            decimal.Round(amount, 2) != amount)
        {
            return new InterbankSendResult(
                InterbankSendOutcome.Failed,
                reference ?? "",
                "Transfer failed: InvalidAmount",
                TransactionResult.InvalidAmount);
        }

        reference ??= Guid.NewGuid().ToString("N");

        var existing = _repository.GetByReference(reference);

        if (existing != null)
        {
            bool sameDetails =
                existing.SenderAccountNumber == senderNumber &&
                existing.RecipientAccountNumber == recipientAccountNumber &&
                existing.BankUrl == bankUrl &&
                existing.Amount == amount;

            if (!sameDetails)
            {
                return new InterbankSendResult(
                    InterbankSendOutcome.ReferenceMismatch,
                    reference,
                    "Transfer reference already used for different details.");
            }

            if (existing.Status == InterbankTransferStatus.Successful)
            {
                return new InterbankSendResult(
                    InterbankSendOutcome.AlreadySuccessful,
                    reference,
                    $"Transfer already successful! Reference: {reference}");
            }

            if (existing.Status == InterbankTransferStatus.Reversed)
            {
                return new InterbankSendResult(
                    InterbankSendOutcome.AlreadyReversed,
                    reference,
                    $"Transfer already reversed. Reference: {reference}");
            }

            return new InterbankSendResult(
                InterbankSendOutcome.Pending,
                reference,
                $"Transfer pending confirmation. Reference: {reference}. No additional debit made.");
        }
        PinAttemptOutcome pinAttempt = _accounts.VerifyPinWithLockout(senderNumber, pin);

        if (pinAttempt == PinAttemptOutcome.Locked)
        {
            int seconds = _accounts.GetLockSecondsRemaining(senderNumber);

            return new InterbankSendResult(
                InterbankSendOutcome.AccountLocked,
                reference,
                $"Too many requests. Please try again in {seconds} seconds.",
                RetryAfterSeconds: seconds);
        }

        if (pinAttempt == PinAttemptOutcome.WrongPin)
        {
            return new InterbankSendResult(
                InterbankSendOutcome.Failed,
                reference,
                $"Transfer failed: {TransactionResult.InvalidPin}",
                TransactionResult.InvalidPin);
        }

        if (pinAttempt == PinAttemptOutcome.AccountNotFound)
        {
            return new InterbankSendResult(
                InterbankSendOutcome.Failed,
                reference,
                $"Transfer failed: {TransactionResult.AccountNotFound}",
                TransactionResult.AccountNotFound);
        }

        var request = new InterbankTransferRequestDto
        {
            Reference = reference,
            SenderAccountNumber = senderNumber,
            RecipientAccountNumber = recipientAccountNumber,
            Amount = amount
        };

        TransactionResult debitResult = _repository.DebitAndCreate(
            senderNumber,
            recipientAccountNumber,
            bankUrl,
            amount,
            pin,
            reference,
            TransactionDescriptions.InterbankTransferTo(recipientAccountNumber));

        if (debitResult == TransactionResult.DuplicateReference)
        {
            return new InterbankSendResult(
                InterbankSendOutcome.DuplicateReference,
                reference,
                $"Transfer reference already exists. Reference: {reference}. No additional debit made.");
        }

        if (debitResult != TransactionResult.Success)
        {
            return new InterbankSendResult(
                InterbankSendOutcome.Failed,
                reference,
                $"Transfer failed: {debitResult}",
                debitResult);
        }

        try
        {
            using HttpResponseMessage response =
                await _bankApiClient.SendTransferAsync(
                    bankUrl,
                    request);

            if (response.IsSuccessStatusCode)
            {
                string responseMessage =
                    await response.Content.ReadAsStringAsync();

                bool confirmed =
                    responseMessage.Contains(
                        InterbankProtocol.TransferReceived,
                        StringComparison.Ordinal) ||
                    responseMessage.Contains(
                        InterbankProtocol.TransferAlreadyProcessed,
                        StringComparison.Ordinal);

                if (confirmed)
                {
                    _repository.MarkSuccessful(reference);

                    return new InterbankSendResult(
                        InterbankSendOutcome.Successful,
                        reference,
                        $"Transfer successful! Reference: {reference}");
                }

                // A successful HTTP response does not necessarily
                // mean the transfer was successful.
                return new InterbankSendResult(
                    InterbankSendOutcome.Pending,
                    reference,
                    $"Transfer pending confirmation. Reference: {reference}.");
            }

            // Do not reverse on HTTP 400 or 404.
            // Only a confirmed final rejection permits reversal.
            _logger.LogWarning(
                "Receiving bank returned HTTP {StatusCode} for transfer {Reference}.",
                response.StatusCode,
                reference);

            return new InterbankSendResult(
                InterbankSendOutcome.Pending,
                reference,
                $"Transfer pending confirmation. Reference: {reference}.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Network error for interbank transfer {Reference}",
                reference);

            return new InterbankSendResult(
                InterbankSendOutcome.Pending,
                reference,
                $"Network error. Transfer pending confirmation. Reference: {reference}.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Timeout for interbank transfer {Reference}",
                reference);

            return new InterbankSendResult(
                InterbankSendOutcome.Pending,
                reference,
                $"Transfer timed out. Pending confirmation. Reference: {reference}.");
        }
    }

    public async Task ReconcilePendingTransfersAsync(
        WorkPartition partition,
        CancellationToken cancellationToken)
    {
        var pendingTransfers = _repository.GetPendingTransfers(partition);

        foreach (var pending in pendingTransfers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using HttpResponseMessage response =
                    await _bankApiClient.GetTransferStatusAsync(
                        pending.BankUrl,
                        pending.Reference);

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var outcome =
                    await response.Content.ReadFromJsonAsync
                        <InterbankTransferOutcomeDto>(
                            cancellationToken: cancellationToken);

                if (outcome == null)
                {
                    continue;
                }

                bool matches =
                    outcome.Reference == pending.Reference &&
                    outcome.SenderAccountNumber ==
                        pending.SenderAccountNumber &&
                    outcome.RecipientAccountNumber ==
                        pending.RecipientAccountNumber &&
                    outcome.Amount == pending.Amount;

                if (!matches)
                {
                    _logger.LogWarning(
                        "Interbank transfer details mismatch: {Reference}",
                        pending.Reference);

                    continue;
                }

                if (outcome.Status == InterbankTransferStatus.Successful)
                {
                    _repository.MarkSuccessful(pending.Reference);

                    _logger.LogInformation(
                        "Interbank transfer confirmed: {Reference}",
                        pending.Reference);
                }
                else if (outcome.Status == InterbankTransferStatus.Rejected)
                {
                    bool reversed = _repository.Reverse(
                        pending.Reference,
                        TransactionDescriptions.InterbankReversal(pending.Reference));

                    if (reversed)
                    {
                        _logger.LogInformation(
                            "Rejected interbank transfer reversed: {Reference}",
                            pending.Reference);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not reverse rejected transfer: {Reference}",
                            pending.Reference);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Interbank transfer still pending: {Reference}",
                        pending.Reference);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Interbank reconciliation failed: {Reference}",
                    pending.Reference);
            }
        }
    }
}
