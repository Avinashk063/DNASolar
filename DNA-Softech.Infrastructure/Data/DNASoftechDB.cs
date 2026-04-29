using Microsoft.EntityFrameworkCore;
using DNASoftech.Domain.Models;
using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Infrastructure.Data.Interface;
using System.Data;

namespace DNASoftech.Infrastructure.Data
{
    public class DNASoftechDB : DbContext, IDNASoftechDB
    {
        public DNASoftechDB(DbContextOptions<DNASoftechDB> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.OriginalPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>().Property(o => o.UnitPrice).HasPrecision(18, 2);

            modelBuilder.Entity<Cart>()
                .HasIndex(c => c.UserId)
                .IsUnique();

            modelBuilder.Entity<CartItem>()
                .HasIndex(ci => new { ci.CartId, ci.ProductId })
                .IsUnique();

            modelBuilder.Entity<Cart>()
                .HasMany(c => c.Items)
                .WithOne(i => i.Cart)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Order>()
                .Property(o => o.PaymentStatus)
                .HasConversion<string>();

            modelBuilder.Entity<Order>()
                .Property(o => o.PaymentMethod)
                .HasConversion<string>();

            // AppUser - shop authentication user table
            // Unique index on Email for fast login lookups
            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Review - FK to Product with cascade delete
            modelBuilder.Entity<Review>()
                .HasOne(r => r.Product)
                .WithMany()
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }

        public IDbConnection Connection => Database.GetDbConnection();

        // Appointment / CRM contacts
        public DbSet<Users> Users { get; set; }

        // Shop catalogue
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }

        // Cart and checkout
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // Wishlist and reviews
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Review> Reviews { get; set; }

        // Shop authentication - distinct from appointment Users
        public DbSet<AppUser> AppUsers { get; set; }
    }
}
