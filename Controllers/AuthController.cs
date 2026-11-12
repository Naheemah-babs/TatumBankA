
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using TatumWelcome2.BLL;
using TatumWelcome2.DTOs;
using TatumWelcome2.Interfaces;
using TatumWelcome2.Models;

namespace TatumWelcome2.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAccountRepository _repository;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAccountRepository repository,
        IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    [HttpPost("/accounts")]
    [Tags("Customer Accounts")]
    public IActionResult CreateAccount(
        [FromBody] CreateAccountRequestDto request)
    {
        try
        {
            Account? account =
                AccountCreationService.CreateAccount(
                    _repository,
                    request.Name,
                    request.Phone,
                    request.Pin,
                    request.City,
                    request.OpeningDeposit);

            if (account == null)
            {
                return BadRequest(new
                {
                    Message =
                        "Invalid account details. Check your name, " +
                        "phone number, PIN, city, and opening " +
                        "deposit between 1000 and the maximum " +
                        "supported balance."
                });
            }

            return Created(
                $"/accounts/{account.AccountNumber}",
                new
                {
                    Message = "Account created successfully.",
                    account.AccountNumber,
                    account.Name,
                    account.Branch,
                    account.Balance
                });
        }
        catch (InvalidOperationException ex)
            when (ex.Message ==
                "This phone number is already registered.")
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    [HttpPost("/auth/login")]
    [EnableRateLimiting("login")]
    [Tags("Customer Authentication")]
    public IActionResult Login(
        [FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.AccountNumber) ||
            string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest(new
            {
                Message = "Account number and PIN are required."
            });
        }

        Account? account = AccountService.Login(
            _repository,
            request.AccountNumber,
            request.Pin);

        if (account == null)
        {
            return Unauthorized(new
            {
                Message = "Invalid account number or PIN."
            });
        }

        string jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is missing.");

        string jwtIssuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is missing.");

        string jwtAudience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is missing.");

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                account.AccountNumber),

            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        DateTime expiresAt =
            DateTime.UtcNow.AddMinutes(30);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        string tokenString =
            new JwtSecurityTokenHandler()
                .WriteToken(token);

        return Ok(new
        {
            Message = "Login successful.",
            account.AccountNumber,
            account.Name,
            account.IsFrozen,
            AccessToken = tokenString,
            TokenType = "Bearer",
            ExpiresAt = expiresAt
        });
    }
}
