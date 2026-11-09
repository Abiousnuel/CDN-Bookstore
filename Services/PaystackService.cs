
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BookHaven.Services
{
    public class PaystackService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        private const string BaseUrl = "https://api.paystack.co";

        public PaystackService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        private string GetSecretKey()
        {
            var secretKey = _configuration["Paystack:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException(
                    "Paystack secret key is missing. Check .NET User Secrets.");
            }

            return secretKey;
        }

        // INITIALIZE PAYMENT
        public async Task<PaystackCheckoutResult> InitializeTransactionAsync(
            decimal amount,
            string email,
            string reference,
            string callbackUrl,
            CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
                throw new ArgumentException(
                    "Payment amount must be greater than zero.");

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException(
                    "Customer email is required.");

            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException(
                    "Payment reference is required.");

            var amountInKobo = checked((long)Math.Round(
                amount * 100m,
                0,
                MidpointRounding.AwayFromZero));

            var payload = new
            {
                email,
                amount = amountInKobo,
                reference,
                callback_url = callbackUrl,
                currency = "NGN"
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/transaction/initialize");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", GetSecretKey());

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(
                request, cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Paystack initialization failed: {responseText}");
            }

            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            if (!root.GetProperty("status").GetBoolean())
            {
                var message = root.TryGetProperty("message", out var error)
                    ? error.GetString()
                    : "Unknown Paystack error.";

                throw new InvalidOperationException(
                    $"Paystack could not initialize payment: {message}");
            }

            var data = root.GetProperty("data");

            var authorizationUrl =
                data.GetProperty("authorization_url").GetString();

            var returnedReference =
                data.GetProperty("reference").GetString();

            if (string.IsNullOrWhiteSpace(authorizationUrl) ||
                string.IsNullOrWhiteSpace(returnedReference))
            {
                throw new InvalidOperationException(
                    "Paystack returned incomplete checkout information.");
            }

            return new PaystackCheckoutResult
            {
                AuthorizationUrl = authorizationUrl,
                Reference = returnedReference
            };
        }

        // VERIFY PAYMENT
        public async Task<PaystackVerificationResult> VerifyTransactionAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException(
                    "Payment reference is required.");

            var encodedReference = Uri.EscapeDataString(reference);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{BaseUrl}/transaction/verify/{encodedReference}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", GetSecretKey());

            using var response = await _httpClient.SendAsync(
                request, cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    "Paystack payment verification failed.");
            }

            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            if (!root.GetProperty("status").GetBoolean())
            {
                throw new InvalidOperationException(
                    "Paystack could not verify this payment.");
            }

            var data = root.GetProperty("data");

            return new PaystackVerificationResult
            {
                Reference = data.GetProperty("reference").GetString() ?? "",
                Status = data.GetProperty("status").GetString() ?? "",
                Amount = data.GetProperty("amount").GetDecimal(),
                Currency = data.GetProperty("currency").GetString() ?? ""
            };
        }
    }

    public class PaystackCheckoutResult
    {
        public string AuthorizationUrl { get; set; } = "";
        public string Reference { get; set; } = "";
    }

    public class PaystackVerificationResult
    {
        public string Reference { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "";
    }
}
