using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.API.Mapping;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Models;
using TatumWelcome2.Security;

namespace TatumWelcome2.API.Controllers;

[ApiController]
[Route("bills")]
[Tags("Bill Payments")]
public class BillPaymentsController : ControllerBase
{
    private readonly IBillPaymentService _service;

    public BillPaymentsController(IBillPaymentService service)
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
        [FromBody] BillPaymentRequest request)
    {
        return BillPaymentResponses.Pay(_service.ProcessPayment(request));
    }
}
