using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TatumWelcome2.API.Mapping;
using TatumWelcome2.API.Security;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Models;
using TatumWelcome2.Security;

namespace TatumWelcome2.API.Controllers;

[ApiController]
[Route("transfers")]
[Tags("Interbank Transfers")]
public class InterbankTransfersController : ControllerBase
{
    private readonly IInterbankTransferService _incoming;
    private readonly IInterbankOutgoingTransferService _outgoing;

    public InterbankTransfersController(
        IInterbankTransferService incoming,
        IInterbankOutgoingTransferService outgoing)
    {
        _incoming = incoming;
        _outgoing = outgoing;
    }

    [Authorize]
    [EnableRateLimiting("pin-transactions")]
    [HttpPost("send")]
    public async Task<IActionResult> Send(
        [FromBody] SendInterbankTransferRequestDto request)
    {
        string? senderNumber = User.GetAccountNumber();

        if (senderNumber is null)
            return Unauthorized();

        return InterbankResponses.Send(
            await _outgoing.SendTransferAsync(senderNumber, request));
    }

    [InterbankApiKey]
    [HttpPost("receive")]
    public IActionResult Receive(
        [FromBody] InterbankTransferRequestDto request)
    {
        return InterbankResponses.Receive(_incoming.ReceiveTransfer(request));
    }

    [InterbankApiKey]
    [HttpGet("status/{reference}")]
    public IActionResult GetStatus(string reference)
    {
        return InterbankResponses.Status(_incoming.GetOutcome(reference));
    }
}
