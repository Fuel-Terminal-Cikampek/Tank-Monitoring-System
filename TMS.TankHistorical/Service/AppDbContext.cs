using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TMS.Models;

namespace TMS.Service
{
    public class AppDbContext :DbContext
    {
        public DbSet<Product> Master_Product { get; set; }
        public DbSet<Tank> Tank { get; set; }
        public DbSet<TankLiveData> Tank_Live_Data { get; set; }
        public DbSet<TankHistorical> Tank_Historical { get; set; }
        public DbSet<TankMovement> Tank_Movement { get; set; }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Tank primary key, table name, and ignore navigation properties
            builder.Entity<Tank>()
                .ToTable("Tank")
                .HasKey(t => t.Tank_Name);

            builder.Entity<Tank>()
                .Ignore(t => t.tankMovement)
                .Ignore(t => t.TankHistoricals)
                .Ignore(t => t.TankTickets);

            // Tank.Product_ID now stores Product_Code (string), not GUID
            // No conversion needed - database already stores as string

            // Configure Product primary key and table name
            builder.Entity<Product>()
                .ToTable("Master_Product")
                .HasKey(p => p.Product_ID);

            // Configure TankLiveData primary key and table name
            builder.Entity<TankLiveData>()
                .ToTable("Tank_LiveData")
                .HasKey(t => t.Tank_Number);

            // TankLiveData.Product_ID now stores Product_Code (string), not GUID
            // No conversion needed - database already stores as string

            // Configure TankHistorical primary key and table name
            builder.Entity<TankHistorical>()
                .ToTable("Tank_HistoricalData")
                .HasKey(t => t.Id);

            // Configure TankMovement primary key and table name
            builder.Entity<TankMovement>()
                .ToTable("Tank_Movement")
                .HasKey(tm => tm.Tank_Number);

            // Product relationship with Tank removed - type mismatch (Tank.Product_ID is string, Product.Product_ID is int)
            // Use manual LINQ joins with .ToString() conversion instead

            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);
        }
    }
}