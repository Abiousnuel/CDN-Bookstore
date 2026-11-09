
using BookHaven.Data;
using BookHaven.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BookHaven.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private const long MaxImageSize = 5 * 1024 * 1024;
        private const long MaxPdfSize = 50 * 1024 * 1024;

        private readonly string[] _allowedImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public AdminController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalBooks = await _context.Books.CountAsync();
            ViewBag.TotalCategories = await _context.Categories.CountAsync();
            ViewBag.ActiveBooks = await _context.Books.CountAsync(b => b.IsActive);
            ViewBag.TotalOrders = await _context.Orders.CountAsync();

            var books = await _context.Books
                .Include(b => b.Category)
                .OrderByDescending(b => b.DateAdded)
                .ToListAsync();

            return View(books);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadCategories();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Book book,
            IFormFile? coverFile,
            IFormFile? pdfFile)
        {
            if (string.IsNullOrWhiteSpace(book.Title))
                ModelState.AddModelError("Title", "Book title is required.");

            if (string.IsNullOrWhiteSpace(book.Author))
                ModelState.AddModelError("Author", "Author is required.");

            if (book.Price <= 0)
                ModelState.AddModelError("Price", "Enter a price greater than zero.");

            if (book.CategoryId <= 0 ||
                !await _context.Categories.AnyAsync(c => c.Id == book.CategoryId))
            {
                ModelState.AddModelError("CategoryId", "Please select a valid category.");
            }

            if (!ValidateCoverFile(coverFile))
            {
                await LoadCategories();
                return View(book);
            }

            if (!await ValidatePdfFileAsync(pdfFile, required: true))
            {
                await LoadCategories();
                return View(book);
            }

            if (ModelState.IsValid)
            {
                book.DateAdded = DateTime.UtcNow;
                book.IsActive = true;

                if (coverFile != null)
                    book.CoverImage = await SaveCoverFile(coverFile);
                else if (string.IsNullOrWhiteSpace(book.CoverImage))
                    book.CoverImage =
                        "https://placehold.co/400x550/102B4E/FFFFFF?text=Book+Cover";

                book.FilePath = await SavePdfFileAsync(pdfFile!);

                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Book added successfully!";
                return RedirectToAction(nameof(Index));
            }

            await LoadCategories();
            return View(book);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var book = await _context.Books.FindAsync(id);

            if (book == null)
                return NotFound();

            await LoadCategories();
            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Book book,
            IFormFile? coverFile,
            IFormFile? pdfFile)
        {
            if (id != book.Id)
                return NotFound();

            if (string.IsNullOrWhiteSpace(book.Title))
                ModelState.AddModelError("Title", "Book title is required.");

            if (string.IsNullOrWhiteSpace(book.Author))
                ModelState.AddModelError("Author", "Author is required.");

            if (book.Price <= 0)
                ModelState.AddModelError("Price", "Enter a price greater than zero.");

            if (book.CategoryId <= 0 ||
                !await _context.Categories.AnyAsync(c => c.Id == book.CategoryId))
            {
                ModelState.AddModelError("CategoryId", "Please select a valid category.");
            }

            if (!ValidateCoverFile(coverFile))
            {
                await LoadCategories();
                return View(book);
            }

            if (!await ValidatePdfFileAsync(pdfFile, required: false))
            {
                await LoadCategories();
                return View(book);
            }

            if (ModelState.IsValid)
            {
                var existingBook = await _context.Books.FindAsync(id);

                if (existingBook == null)
                    return NotFound();

                existingBook.Title = book.Title;
                existingBook.Author = book.Author;
                existingBook.Description = book.Description;
                existingBook.Price = book.Price;
                existingBook.CategoryId = book.CategoryId;
                existingBook.ISBN = book.ISBN;

                if (coverFile != null)
                {
                    var newCover = await SaveCoverFile(coverFile);
                    DeleteCoverFile(existingBook.CoverImage);
                    existingBook.CoverImage = newCover;
                }

                if (pdfFile != null)
                {
                    var newPdf = await SavePdfFileAsync(pdfFile);
                    DeletePdfFile(existingBook.FilePath);
                    existingBook.FilePath = newPdf;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Book updated successfully!";
                return RedirectToAction(nameof(Index));
            }

            await LoadCategories();
            return View(book);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _context.Books
                .Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (book == null)
                return NotFound();

            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var book = await _context.Books.FindAsync(id);

            if (book == null)
                return NotFound();

            bool hasOrders = await _context.OrderItems
                .AnyAsync(i => i.BookId == id);

            var cartItems = await _context.CartItems
                .Where(c => c.BookId == id)
                .ToListAsync();

            _context.CartItems.RemoveRange(cartItems);

            if (hasOrders)
            {
                book.IsActive = false;

                TempData["Success"] =
                    "The book belongs to an existing order, so it was deactivated instead of permanently deleted.";
            }
            else
            {
                DeleteCoverFile(book.CoverImage);
                DeletePdfFile(book.FilePath);

                _context.Books.Remove(book);

                TempData["Success"] = "Book deleted successfully!";
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool ValidateCoverFile(IFormFile? coverFile)
        {
            if (coverFile == null)
                return true;

            if (coverFile.Length == 0)
            {
                ModelState.AddModelError("coverFile", "The selected image is empty.");
                return false;
            }

            if (coverFile.Length > MaxImageSize)
            {
                ModelState.AddModelError("coverFile", "Image size must not exceed 5 MB.");
                return false;
            }

            var extension =
                Path.GetExtension(coverFile.FileName).ToLowerInvariant();

            if (!_allowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "coverFile",
                    "Only JPG, JPEG, PNG and WEBP images are allowed.");

                return false;
            }

            return true;
        }

        private async Task<bool> ValidatePdfFileAsync(
            IFormFile? pdfFile,
            bool required)
        {
            if (pdfFile == null)
            {
                if (required)
                {
                    ModelState.AddModelError(
                        "pdfFile",
                        "Please upload the book's PDF file.");

                    return false;
                }

                return true;
            }

            if (pdfFile.Length == 0)
            {
                ModelState.AddModelError("pdfFile", "The selected PDF is empty.");
                return false;
            }

            if (pdfFile.Length > MaxPdfSize)
            {
                ModelState.AddModelError(
                    "pdfFile",
                    "PDF size must not exceed 50 MB.");

                return false;
            }

            var extension =
                Path.GetExtension(pdfFile.FileName).ToLowerInvariant();

            if (extension != ".pdf")
            {
                ModelState.AddModelError(
                    "pdfFile",
                    "Only PDF files are allowed.");

                return false;
            }

            var signature = new byte[5];

            using var stream = pdfFile.OpenReadStream();

            var bytesRead = await stream.ReadAsync(signature, 0, signature.Length);

            if (bytesRead != 5 ||
                !System.Text.Encoding.ASCII.GetString(signature).Equals("%PDF-"))
            {
                ModelState.AddModelError(
                    "pdfFile",
                    "The uploaded file is not a valid PDF.");

                return false;
            }

            return true;
        }

        private async Task<string> SaveCoverFile(IFormFile coverFile)
        {
            var folder = Path.Combine(
                _environment.WebRootPath,
                "images",
                "books");

            Directory.CreateDirectory(folder);

            var extension =
                Path.GetExtension(coverFile.FileName).ToLowerInvariant();

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await coverFile.CopyToAsync(stream);
            }

            return $"/images/books/{fileName}";
        }

        private async Task<string> SavePdfFileAsync(IFormFile pdfFile)
        {
            var folder = Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "BookFiles");

            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}.pdf";
            var filePath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }

            return fileName;
        }

        private void DeleteCoverFile(string? coverImage)
        {
            if (string.IsNullOrWhiteSpace(coverImage))
                return;

            if (!coverImage.StartsWith(
                "/images/books/",
                StringComparison.OrdinalIgnoreCase))
                return;

            var fileName = Path.GetFileName(coverImage);

            if (string.IsNullOrWhiteSpace(fileName))
                return;

            var filePath = Path.Combine(
                _environment.WebRootPath,
                "images",
                "books",
                fileName);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        private void DeletePdfFile(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            var safeFileName = Path.GetFileName(fileName);

            if (safeFileName != fileName)
                return;

            var filePath = Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "BookFiles",
                safeFileName);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        private async Task LoadCategories()
        {
            ViewBag.Categories = new SelectList(
                await _context.Categories
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id",
                "Name");
        }
    }
}
