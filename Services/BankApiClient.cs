
using System.Net;
using System.Net.Http.Json;
using TatumWelcome2.DTOs;
using TatumWelcome2.Interfaces;

namespace TatumWelcome2.Services;

public class BankApiClient : IBankApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public BankApiClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<AccountLookupDto?> FindAccountAsync(
        string bankUrl,
        string accountNumber)
    {
        string url =
            $"{bankUrl.TrimEnd('/')}/accounts/" +
            Uri.EscapeDataString(accountNumber);

        using HttpRequestMessage message =
            CreateRequest(HttpMethod.Get, url);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(message);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<AccountLookupDto>();
    }

    public async Task<HttpResponseMessage> SendTransferAsync(
        string bankUrl,
        InterbankTransferRequestDto request)
    {
        string url =
            $"{bankUrl.TrimEnd('/')}/transfers/receive";

        using HttpRequestMessage message =
            CreateRequest(HttpMethod.Post, url);

        message.Content = JsonContent.Create(request);

        return await _httpClient.SendAsync(message);
    }

    public async Task<HttpResponseMessage> GetTransferStatusAsync(
        string bankUrl,
        string reference)
    {
        string url =
            $"{bankUrl.TrimEnd('/')}/transfers/status/" +
            Uri.EscapeDataString(reference);

        using HttpRequestMessage message =
            CreateRequest(HttpMethod.Get, url);

        return await _httpClient.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendBillPaymentAsync(
        string bankUrl,
        BillPaymentRequestDto request)
    {
        string url =
            $"{bankUrl.TrimEnd('/')}/bills/pay";

        using HttpRequestMessage message =
            CreateRequest(HttpMethod.Post, url);

        message.Content = JsonContent.Create(request);

        return await _httpClient.SendAsync(message);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url)
    {
        string apiKey =
            _configuration["Interbank:ApiKey"]
            ?? throw new InvalidOperationException(
                "Interbank API key is not configured.");

        var message = new HttpRequestMessage(method, url);

        message.Headers.Add("X-Interbank-Key", apiKey);

        return message;
    }
}
