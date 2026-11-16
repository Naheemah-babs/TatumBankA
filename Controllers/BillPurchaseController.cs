using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TatumWelcome2.API.Mapping;
using TatumWelcome2.API.Security;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;

namespace TatumWelcome2.API.Controllers;

/// <summary>
/// Customer airtime and data purchases. Networks and plans are listed by
/// GET /bills/networks and GET /bills/plans.
/// </summary>
[ApiController]
[Authorize]
[Route("accounts/me")]
[Tags("Customer Bill Payments")]
public class BillPurchasesController : ControllerBase
{
    private readonly ICustomerBillPaymentService _billPayments;

    public BillPurchasesController(ICustomerBillPaymentService billPayments)
    {
        _billPayments = billPayments;
    }

    [HttpPost("airtime")]
    [EnableRateLimiting("pin-transactions")]
    public IActionResult BuyAirtime(
        [FromBody] AirtimePurchaseRequestDto request)
    {
        string? accountNumber = User.GetAccountNumber();

        if (accountNumber is null)
            return Unauthorized();

        return BillPaymentResponses.Purchase(
            _billPayments.BuyAirtime(accountNumber, request));
    }

    [HttpPost("data")]
    [EnableRateLimiting("pin-transactions")]
    public IActionResult BuyData(
        [FromBody] DataPurchaseRequestDto request)
    {
        string? accountNumber = User.GetAccountNumber();

        if (accountNumber is null)
            return Unauthorized();

        return BillPaymentResponses.Purchase(
            _billPayments.BuyData(accountNumber, request));
    }
}
