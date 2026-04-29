using DNASoftech.Application.Interface;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.Settings;
using DNASoftech.Infrastructure.Data;
using DNASoftech.Infrastructure.Repository.ECommerce;
using DNASoftech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DNASoftech.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DNASoftechDB_NPGSQL");

            services.AddDbContext<DNASoftechDB>(options =>
            {
                options.UseNpgsql(connectionString);
            });

            services.Configure<EmailSettings>(configuration.GetSection("Settings:EmailSettings"));
            services.Configure<PaymentGatewaySettings>(configuration.GetSection("Settings:PaymentGatewaySettings"));

            services.AddScoped<IEmailService, SmtpEmailService>();
            services.AddScoped<IPaymentService, PaymentGatewayService>();

            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.AddScoped<ICartRepository, CartRepository>();
            services.AddScoped<IWishlistRepository, WishlistRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();

            return services;
        }
    }
}
