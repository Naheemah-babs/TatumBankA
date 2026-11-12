
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TatumWelcome2.BLL;
using TatumWelcome2.DTOs;
using TatumWelcome2.Security;

namespace TatumWelcome2.Controllers;

public sealed class SendInterbankTransferRequest
{
    public string Reference { get; set; } = "";
    public string RecipientAccountNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Pin { get; set; } = "";
}

[ApiController]
[Route("transfers")]
[Tags("Interbank Transfers")]
public class InterbankTransfersController : ControllerBase
{
    private readonly InterbankTransferService _incoming;
    private readonly InterbankOutgoingTransferService _outgoing;
    private readonly IConfiguration _configuration;

    public InterbankTransfersController(
        InterbankTransferService incoming,
        InterbankOutgoingTransferService outgoing,
        IConfiguration configuration)
    {
        _incoming = incoming;
        _outgoing = outgoing;
        _configuration = configuration;
    }

    [Authorize]
    [EnableRateLimiting("pin-transactions")]
    [HttpPost("send")]
    public async Task<IActionResult> Send(
        [FromBody] SendInterbankTransferRequest request)
    {
        string? senderNumber =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(senderNumber))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            return BadRequest(new
            {
                Message = "Transfer reference is required."
            });
        }

        if (!Guid.TryParseExact(request.Reference, "N", out _))
        {
            return BadRequest(new
            {
                Message = "Transfer reference must be a valid GUID."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest(new
            {
                Message = "PIN is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.RecipientAccountNumber))
        {
            return BadRequest(new
            {
                Message = "Recipient account number is required."
            });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new
            {
                Message = "Amount must be greater than zero."
            });
        }

        string bankUrl =
            _configuration["Banks:BankB:Url"]
            ?? throw new InvalidOperationException(
                "Bank B URL is not configured.");

        string result = await _outgoing.SendTransferAsync(
            senderNumber,
            bankUrl,
            request.RecipientAccountNumber,
            request.Amount,
            request.Pin,
            request.Reference);

        return Ok(new { Message = result });
    }

    [InterbankApiKey]
    [HttpPost("receive")]
    public IActionResult Receive(
        [FromBody] InterbankTransferRequestDto request)
    {
        string result = _incoming.ReceiveTransfer(request);

        return result switch
        {
            "Success" =>
                Ok("Transfer received successfully."),

            "AlreadyProcessed" =>
                Ok("Transfer already processed."),

            "AccountNotFound" =>
                NotFound("Recipient account not found."),

            "ReferenceConflict" =>
                Conflict("Transfer reference conflict."),

            _ =>
                BadRequest("Invalid transfer request.")
        };
    }

    [InterbankApiKey]
    [HttpGet("status/{reference}")]
    public IActionResult GetStatus(string reference)
    {
        InterbankTransferRequestDto? transfer =
            _incoming.FindProcessedTransfer(reference);

        if (transfer == null)
        {
            return NotFound(
                "No processed transfer found for this reference.");
        }

        return Ok(transfer);
    }
}
