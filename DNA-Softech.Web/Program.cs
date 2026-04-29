using DNASoftech.Application.Interface;
using DNASoftech.Application.Service.ECommerce;
using DNASoftech.Domain.Models.ECommerce;
using Microsoft.AspNetCore.DataProtection;
using DNASoftech.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Data Protection
try
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(
            Path.Combine(builder.Environment.ContentRootPath, "keys")))
        .SetApplicationName("DNASoftechApp");
}
catch
{
    builder.Services.AddDataProtection()
        .SetApplicationName("DNASoftechApp");
}

// MVC
builder.Services.AddControllersWithViews();

// Caching
builder.Services.AddMemoryCache();

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Application Services
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IProductService, ProductService>();

// Authentication
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
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

var app = builder.Build();

// Apply migrations
app.ApplyInfrastructureMigrations();

// Global exception handler
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();

        logger.LogError(ex, "Unhandled exception for {Method} {Path}",
            ctx.Request.Method, ctx.Request.Path);

        ctx.Response.StatusCode = ex is InvalidOperationException or System.Data.Common.DbException ? 503 : 500;
        ctx.Response.ContentType = "application/json";

        var msg = ctx.Response.StatusCode == 503
            ? "Database is temporarily unavailable. Please try again in a moment."
            : "An unexpected server error occurred.";

        await ctx.Response.WriteAsJsonAsync(new { error = msg });
    });
});

// HTTPS
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

// Static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Auth
app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();