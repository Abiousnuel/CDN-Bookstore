using Microsoft.AspNetCore.Identity;

namespace BookHaven.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        public DateTime DateRegistered { get; set; } = DateTime.UtcNow;

        public ICollection<CartItem> CartItems { get; set; }
            = new List<CartItem>();

        public ICollection<Order> Orders { get; set; }
            = new List<Order>();
    }
}