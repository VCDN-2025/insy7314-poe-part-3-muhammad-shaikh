using BankPortal.Data;
using BankPortal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankPortal.Controllers;

[ApiController]
[Route("api/employee/payments")]
public class EmployeePaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmployeePaymentsController(AppDbContext db)
    {
        _db = db;
    }

    // Helper: get current logged-in employee from AUTH cookie
    private async Task<User?> GetCurrentEmployeeAsync()
    {
        if (!Request.Cookies.TryGetValue("AUTH", out var uidStr))
            return null;

        if (!int.TryParse(uidStr, out var uid))
            return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid && u.IsEmployee);
        return user;
    }

    private IActionResult EmployeeUnauthorized()
        => Unauthorized(new { error = "Employee login required" });

    // GET: api/employee/payments?status=PendingVerification
    [HttpGet]
    public async Task<IActionResult> GetPayments([FromQuery] string? status)
    {
        var employee = await GetCurrentEmployeeAsync();
        if (employee == null) return EmployeeUnauthorized();

        // default to PendingVerification if nothing passed
        var effectiveStatus = string.IsNullOrWhiteSpace(status)
            ? "PendingVerification"
            : status;

        // whitelist allowed statuses (+ "All" special value)
        var allowed = new[] { "PendingVerification", "Verified", "SubmittedToSwift", "All" };
        if (!allowed.Contains(effectiveStatus))
            return BadRequest(new { error = "Invalid status filter" });

        var query = _db.Payments
            .Include(p => p.User)
            .AsQueryable();

        if (effectiveStatus != "All")
        {
            if (effectiveStatus == "PendingVerification")
            {
                // include legacy rows that might still be "Pending" or null
                query = query.Where(p =>
                    p.Status == "PendingVerification" ||
                    p.Status == "Pending" ||
                    p.Status == null);
            }
            else
            {
                query = query.Where(p => p.Status == effectiveStatus);
            }
        }

        var list = await query
            .OrderBy(p => p.CreatedAt)
            .Select(p => new
            {
                id = p.Id,
                customerUsername = p.User.Username,
                amountCents = p.AmountCents,
                currency = p.Currency,
                provider = p.Provider,
                payeeAccount = p.PayeeAccount,
                swiftBic = p.SwiftBic,
                status = p.Status,
                isVerified = p.IsVerified,
                submittedToSwift = p.SubmittedToSwift,
                createdAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(new { payments = list });
    }

    // POST: api/employee/payments/{id}/verify
    [HttpPost("{id:guid}/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPayment(Guid id)
    {
        var employee = await GetCurrentEmployeeAsync();
        if (employee == null) return EmployeeUnauthorized();

        var payment = await _db.Payments
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound(new { error = "Payment not found" });

        if (payment.SubmittedToSwift)
            return BadRequest(new { error = "Already submitted to SWIFT" });

        if (string.IsNullOrWhiteSpace(payment.PayeeAccount) ||
            string.IsNullOrWhiteSpace(payment.SwiftBic))
        {
            return BadRequest(new { error = "Missing payee account or SWIFT code" });
        }

        payment.IsVerified = true;
        payment.Status = "Verified";
        payment.VerifiedByEmployeeId = employee.Id;
        payment.VerifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    // POST: api/employee/payments/{id}/submit
    [HttpPost("{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitToSwift(Guid id)
    {
        var employee = await GetCurrentEmployeeAsync();
        if (employee == null) return EmployeeUnauthorized();

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound(new { error = "Payment not found" });

        if (!payment.IsVerified)
            return BadRequest(new { error = "Payment must be verified before submitting to SWIFT" });

        if (payment.SubmittedToSwift)
            return BadRequest(new { error = "Already submitted to SWIFT" });

        payment.SubmittedToSwift = true;
        payment.Status = "SubmittedToSwift";
        payment.SubmittedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // real system would call SWIFT API here
        return Ok(new { ok = true });
    }
}
