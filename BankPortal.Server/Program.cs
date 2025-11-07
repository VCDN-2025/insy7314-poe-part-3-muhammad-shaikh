// Program.cs — BankPortal.Server
// Security-focused setup for an ASP.NET Core + React SPA
// Notes: Comments include WHY each control exists, common pitfalls, and a reference for deeper reading.

using BankPortal.Data;
using BankPortal.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ===== Database (SQLite) =====
// EF Core uses parameterised commands by default -> mitigates SQL Injection.
// Keep connection strings out of source control in production (KeyVault/Secrets).
// Ref: OWASP SQL Injection Prevention Cheat Sheet; Microsoft EF Core security notes.
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=bankportal.db"));

// ===== Password hashing =====
// ASP.NET Core PasswordHasher<T> = PBKDF2 with per-user salt + iteration count.
// Do NOT store plaintext or reversible encryption. Do NOT roll your own crypto.
// Ref: OWASP Password Storage Cheat Sheet; Microsoft Identity PasswordHasher.
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// ===== Controllers (return detailed validation errors) =====
// Use AddControllersWithViews so the built-in Antiforgery filter is registered.
// We customise the automatic 400 response to return field-level errors for the SPA.
// Ref: Microsoft Docs - Model validation in ASP.NET Core; Antiforgery in ASP.NET Core.
builder.Services.AddControllersWithViews().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        return new BadRequestObjectResult(new { error = "Invalid input", details = errors });
    };
});

// ===== CORS (same-origin only) =====
// We intentionally do NOT allow cross-origin here (no origins listed).
// This enforces that the SPA and API are served from the same origin (mitigates CSRF/cookie leakage).
// Ref: OWASP Cross-Origin Resource Sharing (CORS) guidance.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy.WithOrigins() // same-origin only (empty = no cross-origin allowed)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ===== Anti-forgery (CSRF) =====
// We rely on the built-in Antiforgery system for POSTs: framework cookie (HttpOnly) + request token in header.
// Client fetches /api/auth/csrf to get a readable token cookie (XSRF-TOKEN), then sends X-CSRF-TOKEN header.
// Keep cookie Secure + SameSite=Lax so it’s not sent cross-site (reduces CSRF risk).
// Ref: OWASP CSRF Cheat Sheet; Microsoft Antiforgery in ASP.NET Core.
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    // IMPORTANT: let the framework issue its own HttpOnly antiforgery cookie.
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.HttpOnly = true;   // framework antiforgery cookie is HttpOnly by design
    o.Cookie.SameSite = SameSiteMode.Lax;
});

// ===== Rate limiting =====
// Throttles brute force on login and reduces DDoS/abuse blast radius.
// Tune limits per endpoint risk profile; consider IP + username partitioning for /login.
// Ref: Microsoft Rate Limiting middleware (ASP.NET Core 8); OWASP Automated Threats.
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("general", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 60; });
    opts.AddFixedWindowLimiter("auth", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 10; });
    opts.AddFixedWindowLimiter("payment", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 30; });
});

var app = builder.Build();

/// ===== DEV error page (helps debug 500s) =====
/// Detailed errors only in Development to avoid leaking internals.
/// Ref: Microsoft Error handling in ASP.NET Core.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// ===== Apply migrations at startup (dev) =====
// Ensures schema is up to date for local dev. In production, run migrations via CI/CD gate.
// ALSO: Seed employee accounts (no self-registration for staff).
// Ref: Microsoft EF Core migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // --- Seed employee users if none exist yet ---
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

    if (!db.Users.Any(u => u.IsEmployee))
    {
        var emp1 = new User
        {
            FullName = "Admin Employee",
            IdNumber = "EMP001",
            AccountNumber = "90000001",
            Username = "employee1",
            IsEmployee = true
        };
        emp1.PasswordHash = hasher.HashPassword(emp1, "Emp@12345");

        var emp2 = new User
        {
            FullName = "Second Employee",
            IdNumber = "EMP002",
            AccountNumber = "90000002",
            Username = "employee2",
            IsEmployee = true
        };
        emp2.PasswordHash = hasher.HashPassword(emp2, "Emp@12345");

        db.Users.AddRange(emp1, emp2);
        db.SaveChanges();
    }
}

// ===== Transport security =====
// HSTS instructs browsers to stick to HTTPS after first visit (mitigates MitM/downgrade).
// Always redirect HTTP -> HTTPS so cookies (Secure) never ride cleartext.
// Ref: OWASP Transport Layer Protection; Microsoft HTTPS & HSTS in ASP.NET Core.
app.UseHsts();
app.UseHttpsRedirection();

// ===== Security headers (Helmet-like) =====
// X-Content-Type-Options: nosniff -> prevent MIME sniffing
// X-Frame-Options: DENY + CSP frame-ancestors 'none' -> anti-clickjacking
// Referrer-Policy: no-referrer -> minimise data leakage
// Permissions-Policy: limit powerful browser features
// CSP: strong default deny; expand if you add CDNs, images, fonts, etc.
// Ref: OWASP Cheat Sheet Series (Clickjacking, CSP); Mozilla Observatory guidance.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'";

    await next();
});

app.UseCors("Default");
app.UseRateLimiter();

// ===== Static site hosting for the SPA (no dev proxy) =====
// In production we serve the prebuilt React bundle from /wwwroot.
// MapControllers BEFORE SPA fallback so /api/* isn’t swallowed by the client router.
// Ref: Microsoft Single-page app (SPA) hosting in ASP.NET Core.
app.UseDefaultFiles();   // serves index.html by default if present
app.UseStaticFiles();    // serves /wwwroot assets

// API routes
app.MapControllers();

// SPA fallback
app.MapFallbackToFile("/index.html");

app.Run();

/* -------------------------------------------------------------------------
   Bibliography
   ------------------------------------------------------------------------- 

Microsoft (2024) ‘Antiforgery in ASP.NET Core’. Available at: https://learn.microsoft.com/aspnet/core/security/anti-request-forgery (Accessed: 10 October 2025).

Microsoft (2024) ‘Enforce HTTPS in ASP.NET Core’. Available at: https://learn.microsoft.com/aspnet/core/security/enforcing-ssl (Accessed: 10 October 2025).

Microsoft (2024) ‘Rate limiting in ASP.NET Core’. Available at: https://learn.microsoft.com/aspnet/core/performance/rate-limit (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: Cross-Site Request Forgery (CSRF) Prevention’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: Content Security Policy’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: SQL Injection Prevention’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: Clickjacking Defense’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: Password Storage’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

*/
