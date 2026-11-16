using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.Domain.Interfaces;

public interface IBankApiClient
{
    Task<AccountLookupDto?> FindAccountAsync(
        string bankUrl,
        string accountNumber);

    Task<HttpResponseMessage> SendTransferAsync(
        string bankUrl,
        InterbankTransferRequestDto request);

    Task<HttpResponseMessage> GetTransferStatusAsync(
        string bankUrl,
        string reference);

   


}
