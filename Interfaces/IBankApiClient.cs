
using TatumWelcome2.DTOs;

namespace TatumWelcome2.Interfaces;

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
