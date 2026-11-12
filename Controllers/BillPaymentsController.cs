
using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.BLL;
using TatumWelcome2.DTOs;
using TatumWelcome2.Models;
using TatumWelcome2.Security;

namespace TatumWelcome2.Controllers;

[ApiController]
[Route("bills")]
[Tags("Bill Payments")]
public class BillPaymentsController : ControllerBase
{
    private readonly BillPaymentService _service;

    public BillPaymentsController(BillPaymentService service)
    {
        _service = service;
    }

    [HttpGet("networks")]
    public IActionResult GetNetworks()
    {
        return Ok(DataPlans.Networks);
    }

    [HttpGet("plans")]
    public IActionResult GetPlans()
    {
        return Ok(DataPlans.DataPlanItems);
    }

    [InterbankApiKey]
    [HttpPost("pay")]
    public IActionResult Pay(
        [FromBody] BillPaymentRequestDto request)
    {
        BillPaymentResponseDto response =
            _service.ProcessPayment(request);

        return Ok(response);
    }
}
