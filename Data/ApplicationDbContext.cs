
using BookHaven.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookHaven.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Book> Books => Set<Book>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<CartItem> CartItems => Set<CartItem>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<PaymentTransaction> PaymentTransactions
            => Set<PaymentTransaction>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Decimal precision
            builder.Entity<Book>()
                .Property(b => b.Price)
                .HasPrecision(18, 2);

            builder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            builder.Entity<OrderItem>()
                .Property(i => i.UnitPrice)
                .HasPrecision(18, 2);

            builder.Entity<PaymentTransaction>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            // Unique constraints
            builder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            builder.Entity<PaymentTransaction>()
                .HasIndex(p => p.Reference)
                .IsUnique();

            builder.Entity<CartItem>()
                .HasIndex(c => new { c.UserId, c.BookId })
                .IsUnique();

            // Relationships
            builder.Entity<Book>()
                .HasOne(b => b.Category)
                .WithMany(c => c.Books)
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CartItem>()
                .HasOne(c => c.Book)
                .WithMany()
                .HasForeignKey(c => c.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<OrderItem>()
                .HasOne(i => i.Book)
                .WithMany()
                .HasForeignKey(i => i.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            // Fixed seed date to prevent changing model warnings
            var seedDate = new DateTime(
                2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Seed categories
            builder.Entity<Category>().HasData(
                new Category
                {
                    Id = 1,
                    Name = "Self Development"
                },
                new Category
                {
                    Id = 2,
                    Name = "Finance"
                },
                new Category
                {
                    Id = 3,
                    Name = "Productivity"
                }
            );

            // Seed books
            builder.Entity<Book>().HasData(
                new Book
                {
                    Id = 1,
                    Title = "Atomic Habits",
                    Author = "James Clear",
                    CategoryId = 1,
                    Price = 4500,
                    Description = "A practical guide to building good habits and breaking bad ones.",
                    CoverImage = "https://placehold.co/400x550/102B4E/FFFFFF?text=Atomic+Habits",
                    DateAdded = seedDate
                },
                new Book
                {
                    Id = 2,
                    Title = "The Psychology of Money",
                    Author = "Morgan Housel",
                    CategoryId = 2,
                    Price = 5000,
                    Description = "Explore the relationship between money, behaviour and decision-making.",
                    CoverImage = "https://placehold.co/400x550/102B4E/FFFFFF?text=Psychology+of+Money",
                    DateAdded = seedDate
                },
                new Book
                {
                    Id = 3,
                    Title = "Deep Work",
                    Author = "Cal Newport",
                    CategoryId = 3,
                    Price = 4000,
                    Description = "Learn how to focus without distraction on cognitively demanding tasks.",
                    CoverImage = "https://placehold.co/400x550/102B4E/FFFFFF?text=Deep+Work",
                    DateAdded = seedDate
                },
                new Book
                {
                    Id = 4,
                    Title = "Rich Dad Poor Dad",
                    Author = "Robert Kiyosaki",
                    CategoryId = 2,
                    Price = 5500,
                    Description = "A popular introduction to financial education and wealth building.",
                    CoverImage = "https://placehold.co/400x550/102B4E/FFFFFF?text=Rich+Dad+Poor+Dad",
                    DateAdded = seedDate
                }
            );
        }
    }
}
