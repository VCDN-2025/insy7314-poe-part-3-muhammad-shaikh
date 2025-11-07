using System.ComponentModel.DataAnnotations;

namespace BankPortal.Dtos;

public class CreatePaymentDto
{
    [RegularExpression(@"^(?:\d{1,10})(?:\.\d{1,2})?$")]
    public string Amount { get; set; } = null!;

    [RegularExpression(@"^(ZAR|USD|EUR|GBP|AUD|CAD|JPY|CNY)$")]
    public string Currency { get; set; } = null!;

    [RegularExpression(@"^(SWIFT)$")]
    public string Provider { get; set; } = "SWIFT";

    [RegularExpression(@"^[0-9]{8,20}$")]
    public string PayeeAccount { get; set; } = null!;

    [RegularExpression(@"^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    public string SwiftBic { get; set; } = null!;

    public Guid IdempotencyKey { get; set; }
}
