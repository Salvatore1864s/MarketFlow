
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Api.Services;

namespace Payment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Buyer")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public PaymentsController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var buyerId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return Unauthorized();
        }

        var payment = await _paymentService.GetByOrderAsync(
            orderId,
            buyerId,
            cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(payment);
    }
}
