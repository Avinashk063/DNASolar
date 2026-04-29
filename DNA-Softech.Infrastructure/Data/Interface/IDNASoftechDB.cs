using Microsoft.EntityFrameworkCore;
using DNASoftech.Domain.Models;
using DNASoftech.Domain.Models.ECommerce;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNASoftech.Infrastructure.Data.Interface
{
    public interface IDNASoftechDB
    {
        public IDbConnection Connection { get; }
        public DbSet<Users> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
    }
}

