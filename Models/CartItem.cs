namespace BookHaven.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        public int BookId { get; set; }

        public Book? Book { get; set; }

        public int Quantity { get; set; } = 1;

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    }
}