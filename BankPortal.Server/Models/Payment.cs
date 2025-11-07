namespace BankPortal.Models
{
    public class Payment
    {
        // existing keys/fields (unchanged)
        public Guid Id { get; set; } = Guid.NewGuid();
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public long AmountCents { get; set; }
        public string Currency { get; set; } = null!;
        public string Provider { get; set; } = "SWIFT";
        public string PayeeAccount { get; set; } = null!;
        public string SwiftBic { get; set; } = null!;
        public string Status { get; set; } = "PendingVerification";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // NEW: employee-side fields (for Task 3)
        // indicates that an employee has verified account + SWIFT
        public bool IsVerified { get; set; } = false;

        // which employee verified it (User with IsEmployee = true)
        public int? VerifiedByEmployeeId { get; set; }
        public User? VerifiedByEmployee { get; set; }

        public DateTime? VerifiedAt { get; set; }

        // submission to SWIFT (your job ends when this is true)
        public bool SubmittedToSwift { get; set; } = false;
        public DateTime? SubmittedAt { get; set; }
    }
}
