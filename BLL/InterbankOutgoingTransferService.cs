
using System.Net;
using System.Net.Http.Json;
using TatumWelcome2.DTOs;
using TatumWelcome2.Interfaces;
using TatumWelcome2.Repositories;

namespace TatumWelcome2.BLL;

public sealed class InterbankOutgoingTransferService
{
    private readonly IBankApiClient _bankApiClient;
    private readonly OutgoingInterbankTransferRepository _repository;
    private readonly ILogger<InterbankOutgoingTransferService> _logger;

    public InterbankOutgoingTransferService(
        IBankApiClient bankApiClient,
        OutgoingInterbankTransferRepository repository,
        ILogger<InterbankOutgoingTransferService> logger)
    {
        _bankApiClient = bankApiClient;
        _repository = repository;
        _logger = logger;
    }

    public async Task<string> SendTransferAsync(
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
            return "Invalid bank or recipient.";
        }

        if (amount <= 0)
            return "Transfer failed: InvalidAmount";

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
                return "Transfer reference already used for different details.";

            if (existing.Status == "Successful")
                return $"Transfer already successful! Reference: {reference}";

            if (existing.Status == "Reversed")
                return $"Transfer already reversed. Reference: {reference}";

            return $"Transfer pending confirmation. Reference: {reference}. No additional debit made.";
        }

        var request = new InterbankTransferRequestDto
        {
            Reference = reference,
            SenderAccountNumber = senderNumber,
            RecipientAccountNumber = recipientAccountNumber,
            Amount = amount
        };

        TransactionResult debitResult;

        try
        {
            debitResult = _repository.DebitAndCreate(
                senderNumber,
                recipientAccountNumber,
                bankUrl,
                amount,
                pin,
                reference);
        }
        catch (MySqlConnector.MySqlException ex)
            when (ex.Number == 1062)
        {
            return $"Transfer reference already exists. Reference: {reference}. No additional debit made.";
        }

        if (debitResult != TransactionResult.Success)
            return $"Transfer failed: {debitResult}";

        try
        {
            using HttpResponseMessage response =
                await _bankApiClient.SendTransferAsync(
                    bankUrl, request);

            if (response.IsSuccessStatusCode)
            {
                string responseMessage =
                    await response.Content.ReadAsStringAsync();

                bool confirmed =
                    responseMessage.Contains(
                        "Transfer received successfully.",
                        StringComparison.Ordinal) ||
                    responseMessage.Contains(
                        "Transfer already processed.",
                        StringComparison.Ordinal);

                if (confirmed)
                {
                    _repository.MarkSuccessful(reference);
                    return $"Transfer successful! Reference: {reference}";
                }

                return $"Transfer pending confirmation. Reference: {reference}.";
            }

            if (response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.NotFound)
            {
                bool reversed = _repository.Reverse(reference);

                return reversed
                    ? $"Transfer failed. Amount reversed. Reference: {reference}"
                    : $"Transfer rejected. Reversal requires attention. Reference: {reference}";
            }

            return $"Transfer pending confirmation. Reference: {reference}.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Network error for interbank transfer {Reference}",
                reference);

            return $"Network error. Transfer pending confirmation. Reference: {reference}.";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Timeout for interbank transfer {Reference}",
                reference);

            return $"Transfer timed out. Pending confirmation. Reference: {reference}.";
        }
    }

    public async Task ReconcilePendingTransfersAsync(
        CancellationToken cancellationToken)
    {
        var pendingTransfers = _repository.GetPendingTransfers();

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
                    continue;

                var confirmed =
                    await response.Content.ReadFromJsonAsync
                        <InterbankTransferRequestDto>(
                            cancellationToken: cancellationToken);

                if (confirmed == null)
                    continue;

                bool matches =
                    confirmed.Reference == pending.Reference &&
                    confirmed.SenderAccountNumber ==
                        pending.SenderAccountNumber &&
                    confirmed.RecipientAccountNumber ==
                        pending.RecipientAccountNumber &&
                    confirmed.Amount == pending.Amount;

                if (!matches)
                {
                    _logger.LogWarning(
                        "Interbank transfer details mismatch: {Reference}",
                        pending.Reference);

                    continue;
                }

                _repository.MarkSuccessful(pending.Reference);

                _logger.LogInformation(
                    "Interbank transfer confirmed: {Reference}",
                    pending.Reference);
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
