using Microsoft.EntityFrameworkCore;
using BankPortal.Models;

namespace BankPortal.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<Payment> Payments => Set<Payment>();
}
