using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TMS.Models;

namespace TMS.AutoPI
{
    public class AppDbContext :DbContext
    {
        public DbSet<Product> Master_Product { get; set; }
        public DbSet<Tank> Tank { get; set; }
        public DbSet<TankLiveData> Tank_Live_Data { get; set; }
        public DbSet<TankTicket> Tank_Ticket { get; set; }
        public DbSet<TankHistorical> Tank_Historical { get; set; }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { 
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Tank primary key and table name
            builder.Entity<Tank>()
                .HasKey(t => t.Tank_Name);

            // Tank.Product_ID now stores Product_Code (string), not GUID
            // No conversion needed - database already stores as string

            // Configure TankLiveData primary key and table name
            builder.Entity<TankLiveData>()
                .HasKey(t => t.Tank_Number);

            // TankLiveData.Product_ID now stores Product_Code (string), not GUID
            // No conversion needed - database already stores as string

            builder.Entity<TankTicket>()
                 .HasOne(b => b.tank)
                 .WithMany(a => a.TankTickets)
                 .HasForeignKey(b => b.Tank_Number);
            // Product relationship with Tank removed - type mismatch (Tank.Product_ID is string, Product.Product_ID is int)
            // Use manual LINQ joins with .ToString() conversion instead

            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);
        }
    }
}