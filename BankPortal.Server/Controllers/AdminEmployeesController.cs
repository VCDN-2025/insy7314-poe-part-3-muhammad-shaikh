using BankPortal.Data;
using BankPortal.Dtos;
using BankPortal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankPortal.Controllers;

[ApiController]
[Route("api/admin/employees")]
public class AdminEmployeesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AdminEmployeesController(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    // Helper: current logged-in employee (from AUTH cookie)
    private async Task<User?> GetCurrentEmployeeAsync()
    {
        if (!Request.Cookies.TryGetValue("AUTH", out var uidStr))
            return null;

        if (!int.TryParse(uidStr, out var uid))
            return null;

        return await _db.Users.FirstOrDefaultAsync(u => u.Id == uid && u.IsEmployee);
    }

    private IActionResult EmployeeUnauthorized()
        => Unauthorized(new { error = "Employee login required" });

    // POST: /api/admin/employees
    // Only logged-in employees can call this (admin/staff creates more employees)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee([FromBody] RegisterDto dto)
    {
        var current = await GetCurrentEmployeeAsync();
        if (current == null)
            return EmployeeUnauthorized();

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // enforce unique username
        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
            return Conflict(new { error = "Username already exists" });

        // build employee user
        var employee = new User
        {
            FullName = dto.FullName,
            IdNumber = dto.IdNumber,
            AccountNumber = dto.AccountNumber,
            Username = dto.Username,
            IsEmployee = true
        };

        employee.PasswordHash = _hasher.HashPassword(employee, dto.Password);

        _db.Users.Add(employee);
        await _db.SaveChangesAsync();

        return Created(string.Empty, new
        {
            ok = true,
            employee = new
            {
                employee.Id,
                employee.Username,
                employee.FullName,
                employee.IsEmployee
            }
        });
    }
}
