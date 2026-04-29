using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DNASoftech.Infrastructure
{
    public static class ApplicationBuilderExtensions
    {
        public static void ApplyInfrastructureMigrations(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DNASoftechDB>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("InfrastructureMigration");

            try
            {
                db.Database.Migrate();
                logger.LogInformation("Database migration applied successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed. The app will continue but DB operations may fail.");
            }

            // Seed default admin AppUser
            var adminEmail = "Admin@dnsoftech.com";
            try
            {
                var existingAdmin = db.AppUsers.FirstOrDefault(u =>
                    u.Email.ToLower() == adminEmail.ToLower());

                if (existingAdmin == null)
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    var pwdBytes = System.Text.Encoding.UTF8.GetBytes("Admin@123");
                    var hash = sha.ComputeHash(pwdBytes);
                    db.AppUsers.Add(new AppUser
                    {
                        Name = "Default Admin",
                        Email = adminEmail.ToLower(),
                        PasswordHash = Convert.ToHexString(hash),
                        Role = "Admin"
                    });
                    db.SaveChanges();
                    logger.LogInformation("Default admin AppUser created: {Email}", adminEmail);
                }
            }
            catch (Exception se)
            {
                logger.LogWarning(se, "Failed to seed admin AppUser (non-fatal).");
            }

            // Back-fill Product.ImageUrl from ProductImages table for any products
            // whose ImageUrl is a dead local path (e.g. /images/products/xxx.jpg).
            try
            {
                var productsWithStaleUrl = db.Products
                    .Where(p => p.ImageUrl != null && !p.ImageUrl.StartsWith("data:") && !p.ImageUrl.StartsWith("http"))
                    .ToList();

                foreach (var prod in productsWithStaleUrl)
                {
                    var uploadedImage = db.ProductImages
                        .Where(pi => pi.ProductId == prod.ProductId)
                        .OrderBy(pi => pi.SortOrder)
                        .FirstOrDefault();

                    if (uploadedImage != null && !string.IsNullOrEmpty(uploadedImage.ImageUrl))
                    {
                        prod.ImageUrl = uploadedImage.ImageUrl;
                        prod.ImageMimeType = uploadedImage.ImageMimeType;
                    }
                }

                if (productsWithStaleUrl.Any())
                {
                    db.SaveChanges();
                    logger.LogInformation("Back-filled ImageUrl for {Count} product(s) from ProductImages table.", productsWithStaleUrl.Count);
                }
            }
            catch (Exception bfEx)
            {
                logger.LogWarning(bfEx, "Product ImageUrl back-fill failed (non-fatal).");
            }
        }
    }
}
