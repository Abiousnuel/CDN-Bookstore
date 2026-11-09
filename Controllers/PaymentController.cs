
using System.Security.Claims;
using BookHaven.Data;
using BookHaven.Models;
using BookHaven.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookHaven.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PaystackService _paystack;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            PaystackService paystack,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _paystack = paystack;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Confirm(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                ViewBag.PaymentSuccess = false;
                ViewBag.PaymentMessage =
                    "Payment reference was not provided.";

                return View();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            var payment = await _context.PaymentTransactions
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p =>
                    p.Reference == reference &&
                    p.Provider == "Paystack");

            if (payment == null || payment.Order == null)
                return NotFound("Payment record not found.");

            if (payment.Order.UserId != userId)
                return Forbid();

            try
            {
                var result = await _paystack.VerifyTransactionAsync(
                    reference);

                if (result.Reference != reference)
                {
                    ViewBag.PaymentSuccess = false;
                    ViewBag.PaymentMessage =
                        "Payment verification failed. Please contact support.";

                    return View();
                }

                // Paystack returns amounts in kobo.
                var expectedAmountInKobo = payment.Amount * 100m;

                if (result.Amount != expectedAmountInKobo ||
                    result.Currency != "NGN")
                {
                    payment.Status = "AmountMismatch";
                    await _context.SaveChangesAsync();

                    ViewBag.PaymentSuccess = false;
                    ViewBag.PaymentMessage =
                        "The payment amount or currency could not be confirmed. Please contact support.";

                    return View();
                }

                if (result.Status == "success")
                {
                    payment.Status = "Paid";
                    payment.DateVerified = DateTime.UtcNow;

                    payment.Order.Status = OrderStatus.Paid;

                   await _context.SaveChangesAsync();

// Redirect to Downloads with a success indicator.
return RedirectToAction(
    "Index",
    "Downloads",
    new { paymentSuccess = true });
                }

                payment.Status = result.Status;

                if (result.Status == "failed")
                {
                    payment.Order.Status = OrderStatus.Failed;
                }

                await _context.SaveChangesAsync();

                ViewBag.PaymentSuccess = false;
                ViewBag.PaymentMessage =
                    "Your payment has not been confirmed. Please check again or contact support.";

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Paystack verification failed for reference {Reference}",
                    reference);

                ViewBag.PaymentSuccess = false;
                ViewBag.PaymentMessage =
                    "We could not verify your payment right now. Please try again later.";

                return View();
            }
        }
    }
}
