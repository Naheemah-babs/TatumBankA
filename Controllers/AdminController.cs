
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TatumWelcome2.BLL;
using TatumWelcome2.Models;

namespace TatumWelcome2.Controllers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        IConfiguration configuration =
            context.HttpContext.RequestServices
                .GetRequiredService<IConfiguration>();

        string expectedKey =
            configuration["Admin:ApiKey"]
            ?? throw new InvalidOperationException(
                "Admin API key is missing.");

        string suppliedKey =
            context.HttpContext.Request.Headers[
                "X-Admin-Key"].ToString();

        byte[] expectedBytes =
            Encoding.UTF8.GetBytes(expectedKey);

        byte[] suppliedBytes =
            Encoding.UTF8.GetBytes(suppliedKey);

        if (expectedBytes.Length != suppliedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                suppliedBytes))
        {
            context.Result = new UnauthorizedResult();
        }

        return Task.CompletedTask;
    }
}

public sealed class PaymentModeRequest
{
    public string Mode { get; set; } = "";
}

[ApiController]
[Route("admin")]
[Tags("Admin")]
[AdminApiKey]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly BillPaymentService _billPaymentService;

    public AdminController(
        AdminService adminService,
        BillPaymentService billPaymentService)
    {
        _adminService = adminService;
        _billPaymentService = billPaymentService;
    }

    [HttpGet("accounts/{accountNumber}")]
    public IActionResult GetAccount(string accountNumber)
    {
        Account? account =
            _adminService.FindAccount(accountNumber);

        if (account == null)
            return NotFound(new { Message = "Account not found." });

        return Ok(new
        {
            account.AccountNumber,
            account.Name,
            account.IsFrozen
        });
    }

    [HttpPut("accounts/{accountNumber}/freeze")]
    public IActionResult FreezeAccount(string accountNumber)
    {
        Account? account =
            _adminService.FindAccount(accountNumber);

        if (account == null)
            return NotFound(new { Message = "Account not found." });

        if (account.IsFrozen)
            return Ok(new { Message = "Account is already frozen." });

        bool successful =
            _adminService.FreezeAccount(accountNumber);

        if (!successful)
        {
            return Conflict(new
            {
                Message = "Account status could not be updated."
            });
        }

        return Ok(new
        {
            Message = "Account frozen successfully.",
            AccountNumber = accountNumber
        });
    }

    [HttpPut("accounts/{accountNumber}/unfreeze")]
    public IActionResult UnfreezeAccount(string accountNumber)
    {
        Account? account =
            _adminService.FindAccount(accountNumber);

        if (account == null)
            return NotFound(new { Message = "Account not found." });

        if (!account.IsFrozen)
            return Ok(new { Message = "Account is already active." });

        bool successful =
            _adminService.UnfreezeAccount(accountNumber);

        if (!successful)
        {
            return Conflict(new
            {
                Message = "Account status could not be updated."
            });
        }

        return Ok(new
        {
            Message = "Account unfrozen successfully.",
            AccountNumber = accountNumber
        });
    }

    [HttpGet("payment-mode")]
    public IActionResult GetPaymentMode()
    {
        return Ok(new
        {
            PaymentMode = _billPaymentService.PaymentMode
        });
    }

    [HttpPut("payment-mode")]
    public IActionResult SetPaymentMode(
        [FromBody] PaymentModeRequest request)
    {
        bool successful =
            _billPaymentService.SetPaymentMode(request.Mode);

        if (!successful)
        {
            return BadRequest(new
            {
                Message =
                    "Invalid mode. Choose Random, Success, or Failure."
            });
        }

        return Ok(new
        {
            Message = "Payment mode updated successfully.",
            PaymentMode = _billPaymentService.PaymentMode
        });
    }
}
