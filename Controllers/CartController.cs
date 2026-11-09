
using BookHaven.Data;
using BookHaven.Models;
using BookHaven.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookHaven.Controllers
{
    [Authorize]
public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PaystackService _paystack;
        private readonly ILogger<CartController> _logger;

        public CartController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            PaystackService paystack,
            ILogger<CartController> logger)
        {
            _context = context;
            _userManager = userManager;
            _paystack = paystack;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(
            [FromBody] CheckoutRequest request)
        {
            if (request?.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Your shopping cart is empty."
                });
            }

            if (request.Items.Any(i =>
                i.BookId <= 0 || i.Quantity < 1 || i.Quantity > 20))
            {
                return BadRequest(new
                {
                    message = "Your cart contains invalid quantities or books."
                });
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return Unauthorized(new
                {
                    message = "Please log in before checkout."
                });
            }

            var groupedItems = request.Items
                .GroupBy(i => i.BookId)
                .Select(g => new CheckoutItem
                {
                    BookId = g.Key,
                    Quantity = g.Sum(i => i.Quantity)
                })
                .ToList();

            if (groupedItems.Any(i => i.Quantity > 20))
            {
                return BadRequest(new
                {
                    message = "Maximum quantity per book is 20."
                });
            }

            var bookIds = groupedItems.Select(i => i.BookId).ToList();

            var books = await _context.Books
                .Where(b => bookIds.Contains(b.Id) && b.IsActive)
                .ToListAsync();

            if (books.Count != bookIds.Count)
            {
                return BadRequest(new
                {
                    message = "One or more books are unavailable. Please refresh your cart."
                });
            }

            decimal total = 0;

            foreach (var item in groupedItems)
            {
                var book = books.First(b => b.Id == item.BookId);
                total += book.Price * item.Quantity;
            }

            var orderNumber =
                "CDN-" + Guid.NewGuid().ToString("N").ToUpper();

            var paymentReference =
                "CDNPAY-" + Guid.NewGuid().ToString("N").ToUpper();

            var order = new Order
            {
                OrderNumber = orderNumber,
                UserId = user.Id,
                TotalAmount = total,
                Status = OrderStatus.Pending,
                DateCreated = DateTime.UtcNow,
                Items = groupedItems.Select(item =>
                {
                    var book = books.First(b => b.Id == item.BookId);

                    return new OrderItem
                    {
                        BookId = book.Id,
                        BookTitle = book.Title,
                        UnitPrice = book.Price,
                        Quantity = item.Quantity
                    };
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            try
            {
                var redirectUrl = Url.Action(
                    "Confirm",
                    "Payment",
                    null,
                    Request.Scheme
                );

                if (string.IsNullOrWhiteSpace(redirectUrl))
                {
                    throw new InvalidOperationException(
                        "Could not create the payment return URL.");
                }
                _logger.LogInformation(
    "Paystack callback URL: {CallbackUrl}",
    redirectUrl);

                var payment = await _paystack.InitializeTransactionAsync(
                    total,
                    user.Email,
                    paymentReference,
                    redirectUrl
                );

                var transaction = new PaymentTransaction
                {
                    OrderId = order.Id,
                    Reference = paymentReference,
                    Provider = "Paystack",
                    Amount = total,
                    Status = "Pending",
                    DateCreated = DateTime.UtcNow
                };

                _context.PaymentTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    checkoutUrl = payment.AuthorizationUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Paystack checkout initialization failed for order {OrderNumber}",
                    order.OrderNumber
                );

                order.Status = OrderStatus.Failed;
                await _context.SaveChangesAsync();

                return StatusCode(502, new
                {
                    message = "We could not start your payment. Please try again."
                });
            }
        }
    }

    public class CheckoutRequest
    {
        public List<CheckoutItem> Items { get; set; } = new();
    }

    public class CheckoutItem
    {
        public int BookId { get; set; }

        public int Quantity { get; set; }
    }
}
