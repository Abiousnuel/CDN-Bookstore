namespace BookHaven.Models
{
    public enum OrderStatus
    {
        Pending,
        Paid,
        Failed,
        Cancelled
    }

    public class Order
    {
        public int Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        public decimal TotalAmount { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public ICollection<OrderItem> Items { get; set; }
            = new List<OrderItem>();

        public ICollection<PaymentTransaction> Payments { get; set; }
            = new List<PaymentTransaction>();
    }
}