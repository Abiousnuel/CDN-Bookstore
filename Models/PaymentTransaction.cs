namespace BookHaven.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order? Order { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Provider { get; set; } = "Paystack";

        public decimal Amount { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public DateTime? DateVerified { get; set; }
    }
}