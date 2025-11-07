using BankPortal.Data;
using BankPortal.Dtos;
using BankPortal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankPortal.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private static readonly Dictionary<Guid, object> IdemCache = new();

    public PaymentsController(AppDbContext db) { _db = db; }

    private async Task<User?> CurrentUser()
    {
        if (!Request.Cookies.TryGetValue("AUTH", out var uidStr)) return null;
        if (!int.TryParse(uidStr, out var uid)) return null;
        return await _db.Users.FindAsync(uid);
    }

    [HttpGet]
    public async Task<IActionResult> MyPayments()
    {
        var user = await CurrentUser();
        if (user == null) return Unauthorized();
        var items = await _db.Payments
            .Where(p => p.UserId == user.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new {
                p.Id,
                p.AmountCents,
                p.Currency,
                p.Provider,
                p.PayeeAccount,
                p.SwiftBic,
                p.Status,
                p.CreatedAt
            }).ToListAsync();
        return Ok(new { payments = items });
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto)
    {
        if (!TryValidateModel(dto)) return BadRequest("Invalid input");

        var user = await CurrentUser();
        if (user == null) return Unauthorized();

        if (dto.IdempotencyKey == Guid.Empty) return BadRequest("Idempotency required");
        if (IdemCache.TryGetValue(dto.IdempotencyKey, out var cached)) return Ok(cached);

        if (!decimal.TryParse(dto.Amount, out var amount) || amount <= 0)
            return BadRequest("Invalid amount");

        var payment = new Payment
        {
            UserId = user.Id,
            AmountCents = (long)Math.Round(amount * 100M),
            Currency = dto.Currency,
            Provider = dto.Provider,
            PayeeAccount = dto.PayeeAccount,
            SwiftBic = dto.SwiftBic
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var result = new { ok = true, payment = new { payment.Id, payment.Status } };
        IdemCache[dto.IdempotencyKey] = result;
        return Created("", result);
    }
}
