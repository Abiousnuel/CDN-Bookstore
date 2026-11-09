
using BookHaven.Data;
using BookHaven.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookHaven.Controllers
{
    [Authorize]
    public class DownloadsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DownloadsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // Display books purchased by the logged-in customer
        [HttpGet]
        public async Task<IActionResult> Index(bool paymentSuccess = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var orders = await _context.Orders
                .Where(o => o.UserId == userId &&
                            o.Status == OrderStatus.Paid)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Book)
                .ToListAsync();

            var books = orders
                .SelectMany(o => o.Items)
                .Where(i => i.Book != null &&
                            !string.IsNullOrWhiteSpace(i.Book.FilePath))
                .Select(i => i.Book!)
                .GroupBy(b => b.Id)
                .Select(g => g.First())
                .ToList();
            ViewBag.PaymentSuccess = paymentSuccess;
            return View(books);
        }

        // Download a book only if the customer has paid for it
        [HttpGet]
        public async Task<IActionResult> Download(int bookId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var hasPurchased = await _context.Orders
                .AnyAsync(o =>
                    o.UserId == userId &&
                    o.Status == OrderStatus.Paid &&
                    o.Items.Any(i => i.BookId == bookId));

            if (!hasPurchased)
            {
                return Forbid();
            }

            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.Id == bookId);

            if (book == null ||
                string.IsNullOrWhiteSpace(book.FilePath))
            {
                return NotFound("The book PDF is not available.");
            }

            var fileName = Path.GetFileName(book.FilePath);

            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName != book.FilePath)
            {
                return NotFound();
            }

            var filePath = Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "BookFiles",
                fileName
            );

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("The book file could not be found.");
            }

            var safeTitle = string.Join(
                "_",
                book.Title.Split(Path.GetInvalidFileNameChars())
            );

            return PhysicalFile(
                filePath,
                "application/pdf",
                $"{safeTitle}.pdf"
            );
        }
    }
}
