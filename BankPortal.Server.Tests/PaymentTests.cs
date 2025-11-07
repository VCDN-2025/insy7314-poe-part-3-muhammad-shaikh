using BankPortal.Models;
using Xunit;

namespace BankPortal.Server.Tests;

public class PaymentTests
{
    [Fact]
    public void New_payment_has_pending_status_and_not_submitted()
    {
        var p = new Payment
        {
            UserId = 1,
            AmountCents = 10000,
            Currency = "ZAR",
            Provider = "SWIFT",
            PayeeAccount = "12345678",
            SwiftBic = "ABCDZAJJ"
        };

        Assert.Equal("PendingVerification", p.Status);
        Assert.False(p.IsVerified);
        Assert.False(p.SubmittedToSwift);
    }
}
