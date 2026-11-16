using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.BLL.DTOs;

namespace TatumWelcome2.API.Mapping
{
    internal static class ApiResponse
    {

        public static MessageResponseDto Message(string message) =>

            new() { Message = message };


        public static IActionResult BadRequest(string message) =>

            new BadRequestObjectResult(Message(message));


        public static IActionResult NotFound(string message) =>

            new NotFoundObjectResult(Message(message));


        public static IActionResult Conflict(string message) =>

            new ConflictObjectResult(Message(message));


        public static IActionResult Unauthorized(string message) =>

            new UnauthorizedObjectResult(Message(message));


        public static IActionResult ServiceUnavailable(string message) =>

            new ObjectResult(Message(message))

            {

                StatusCode = StatusCodes.Status503ServiceUnavailable

            };

    }
}
