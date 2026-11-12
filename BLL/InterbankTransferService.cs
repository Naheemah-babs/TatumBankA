
using TatumWelcome2.DTOs;
using TatumWelcome2.Repositories;

namespace TatumWelcome2.BLL;

public class InterbankTransferService
{
    private readonly IncomingInterbankTransferRepository _repository;

    public InterbankTransferService(
        IncomingInterbankTransferRepository repository)
    {
        _repository = repository;
    }

    public string ReceiveTransfer(
        InterbankTransferRequestDto request)
    {
        return _repository.ReceiveTransfer(request);
    }

    public InterbankTransferRequestDto? FindProcessedTransfer(
        string reference)
    {
        return _repository.FindProcessedTransfer(reference);
    }
}
