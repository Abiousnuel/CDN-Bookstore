using BookHaven.Data;
using BookHaven.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BookHaven.Services;

var builder = WebApplication.CreateBuilder(args);

// Database connection
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION");
    Console.WriteLine("DefaultConnection exists: " +
    !string.IsNullOrWhiteSpace(
        builder.Configuration.GetConnectionString("DefaultConnection")));

Console.WriteLine("DATABASE_URL exists: " +
    !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable("DATABASE_URL")));

        Console.WriteLine("RAILWAY_TEST: " +
    Environment.GetEnvironmentVariable("RAILWAY_TEST"));

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is missing. Check Railway variables.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Identity and user accounts
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Authentication pages (we will build these later)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddHttpClient<PaystackService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = { "Admin", "Customer" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var email = app.Configuration["AdminSetup:Email"]?.Trim();
    var password = app.Configuration["AdminSetup:Password"];

    if (!string.IsNullOrWhiteSpace(email) &&
        !string.IsNullOrWhiteSpace(password))
    {
        var admin = await userManager.FindByEmailAsync(email);

        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "BookHaven Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, password);

            if (!result.Succeeded)
            {
                throw new Exception(string.Join("; ",
                    result.Errors.Select(e => e.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}


// Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();