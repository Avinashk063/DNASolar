using DNASoftech.Application.Interface;
using DNASoftech.Application.Service.ECommerce;
using DNASoftech.Domain.Models.ECommerce;
using Microsoft.AspNetCore.DataProtection;
using DNASoftech.Infrastructure;

// ─────────────────────────────────────────────────────────────────────────────
// Builder
// ─────────────────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

// Data Protection — persist keys so auth cookies survive restarts
try
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(
            Path.Combine(builder.Environment.ContentRootPath, "keys")))
        .SetApplicationName("DNASoftechApp");
}
catch
{
    // Fallback when FileSystem key persistence is unavailable (e.g. read-only containers)
    builder.Services.AddDataProtection()
        .SetApplicationName("DNASoftechApp");
}

// ── MVC (Controllers + Razor Views) ─────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── In-memory cache (used for OTP storage) ──────────────────────────────────
builder.Services.AddMemoryCache();

// ── Infrastructure registrations (DbContext, repositories, external services) ─
builder.Services.AddInfrastructure(builder.Configuration);

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IProductService, ProductService>();

// ── Cookie authentication ────────────────────────────────────────────────────
// Using a simple, custom cookie scheme — no ASP.NET Identity dependency.
// Login page is the Razor view at /Account/Login (not a static HTML file).
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Name = "DNASession";
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        // Unauthenticated requests to [Authorize] endpoints redirect here
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

// ─────────────────────────────────────────────────────────────────────────────
// App pipeline
// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Auto-apply pending migrations and seed data on startup.
app.ApplyInfrastructureMigrations();

// Always use a clean JSON error handler
// Full exception details are written to the server log only.
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

        ctx.Response.StatusCode = ex is InvalidOperationException or System.Data.Common.DbException ? 503 : 500;
        ctx.Response.ContentType = "application/json";
        var msg = ctx.Response.StatusCode == 503
            ? "Database is temporarily unavailable. Please try again in a moment."
            : "An unexpected server error occurred.";
        await ctx.Response.WriteAsJsonAsync(new { error = msg });
    });
});

// HTTPS redirection
var httpsPortEnv = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")
                   ?? Environment.GetEnvironmentVariable("HTTPS_PORT");
if (!string.IsNullOrEmpty(httpsPortEnv) || app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
else
{
    app.Services.GetRequiredService<ILogger<Program>>()
       .LogWarning("Skipping HTTPS redirection — no HTTPS port configured.");
}

// Serve default files like index.html from wwwroot
app.UseDefaultFiles();

// Serve wwwroot/ (css, js, images, lib)
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// ── MVC default route ────────────────────────────────────────────────────────
// Handles all page requests: /Shop, /Checkout, /Admin, /Account/Login, etc.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ── API controllers ───────────────────────────────────────────────────────────
// Keep the existing JSON API routes: /api/products, /api/orders, /api/admin/*, etc.
app.MapControllers();

app.Run();
