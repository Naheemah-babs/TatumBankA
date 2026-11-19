using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TatumWelcome2.API.Mapping;
using TatumWelcome2.API.Security;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;

namespace TatumWelcome2.API.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IAccountCreationService _accountCreationService;
    private readonly IJwtTokenService _tokenService;

    public AuthController(
        IAccountService accountService,
        IAccountCreationService accountCreationService,
        IJwtTokenService tokenService)
    {
        _accountService = accountService;
        _accountCreationService = accountCreationService;
        _tokenService = tokenService;
    }

    [HttpPost("/accounts")]
    [EnableRateLimiting("create-account")]
    [Tags("Customer Accounts")]
    public IActionResult CreateAccount(
        [FromBody] CreateAccountRequestDto request)
    {
        return AuthResponses.AccountCreated(
            _accountCreationService.CreateAccount(request));
    }

    [HttpPost("/auth/login")]
    [EnableRateLimiting("login")]
    [Tags("Customer Authentication")]
    public IActionResult Login(
        [FromBody] LoginRequestDto request)
    {
        LoginResult result = _accountService.Login(
            request.AccountNumber,
            request.Pin);

        if (result.Account is null)
            return AuthResponses.LoginFailed(result);

        AccessToken token = _tokenService.CreateToken(result.Account.AccountNumber);

        return AuthResponses.LoginSucceeded(result.Account, token);
    }
}
