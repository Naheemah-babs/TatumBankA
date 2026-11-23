using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

/// <summary>Transfers received from other banks.</summary>
public sealed class InterbankTransferService : IInterbankTransferService
{
    private readonly IIncomingInterbankTransferRepository _repository;

    public InterbankTransferService(
        IIncomingInterbankTransferRepository repository)
    {
        _repository = repository;
    }

    public IncomingTransferResult ReceiveTransfer(
        InterbankTransferRequestDto request)
    {
        if (!IsValid(request))
            return IncomingTransferResult.InvalidRequest;

        IncomingTransferWrite write = _repository.ReceiveTransfer(
            request,
            TransactionDescriptions.InterbankTransferFrom(request.SenderAccountNumber));

        return write.Status switch
        {
            IncomingTransferWriteStatus.Credited =>
                IncomingTransferResult.Success,

            IncomingTransferWriteStatus.RecipientRejected =>
                IncomingTransferResult.Rejected,

            _ => FromExisting(write.Existing, request)
        };
    }

    public IncomingTransferOutcome? GetOutcome(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        return _repository.GetOutcome(reference);
    }

    public InterbankTransferRequestDto? FindProcessedTransfer(string reference)
    {
        IncomingTransferOutcome? outcome = GetOutcome(reference);

        if (outcome == null ||
            outcome.Status != InterbankTransferStatus.Successful)
        {
            return null;
        }

        return new InterbankTransferRequestDto
        {
            Reference = outcome.Reference,
            SenderAccountNumber = outcome.SenderAccountNumber,
            RecipientAccountNumber = outcome.RecipientAccountNumber,
            Amount = outcome.Amount
        };
    }

    /// <summary>Replay of a known reference: same details report the stored status.</summary>
    private static IncomingTransferResult FromExisting(
        IncomingTransferOutcome? previous,
        InterbankTransferRequestDto request)
    {
        if (previous == null)
            return IncomingTransferResult.InvalidRequest;

        bool sameRequest =
            previous.SenderAccountNumber == request.SenderAccountNumber &&
            previous.RecipientAccountNumber == request.RecipientAccountNumber &&
            previous.Amount == request.Amount;

        if (!sameRequest)
            return IncomingTransferResult.ReferenceConflict;

        return previous.Status switch
        {
            InterbankTransferStatus.Successful => IncomingTransferResult.AlreadyProcessed,
            InterbankTransferStatus.Rejected => IncomingTransferResult.Rejected,
            _ => IncomingTransferResult.Pending
        };
    }

    private static bool IsValid(InterbankTransferRequestDto? request)
    {
        return request != null &&
               !string.IsNullOrWhiteSpace(request.Reference) &&
               request.Reference.Length <= 100 &&
               !string.IsNullOrWhiteSpace(request.SenderAccountNumber) &&
               request.SenderAccountNumber.Length <= 10 &&
               !string.IsNullOrWhiteSpace(request.RecipientAccountNumber) &&
               request.RecipientAccountNumber.Length <= 10 &&
               AccountLimits.CheckAmount(request.Amount) is null;
    }
}
