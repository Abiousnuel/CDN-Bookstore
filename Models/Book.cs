namespace BookHaven.Models
{
    public class Book
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string CoverImage { get; set; } = string.Empty;

        public string? FilePath { get; set; }

        public string? ISBN { get; set; }

        public int CategoryId { get; set; }

        public Category? Category { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    }
}