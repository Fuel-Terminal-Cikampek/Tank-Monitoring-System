using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TMS.Models;

namespace TMS.ATGService
{
    public class AppDbContext :DbContext
    {
        public DbSet<Product> Master_Product { get; set; }
        public DbSet<Tank> Tank { get; set; }
        public DbSet<TankLiveData> Tank_Live_Data { get; set; }
        public DbSet<TankLiveDataTMS> Tank_LiveDataTMS { get; set; }  // ✅ NEW: Clean version for TMS only
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Tank primary key and table name
            builder.Entity<Tank>()
                .ToTable("Tank")  // Specify actual table name
                .HasKey(t => t.Tank_Name);

            // Ignore Tank-TankMovement navigation - not needed for ATGService
            builder.Entity<Tank>()
                .Ignore(t => t.tankMovement);

            // Configure Product table name
            builder.Entity<Product>()
                .ToTable("Product");

            // Tank.Product_ID now stores Product_Code (string), not GUID
            // No conversion needed - database already stores as string

            // Configure TankLiveData primary key and table name
            builder.Entity<TankLiveData>()
                .ToTable("Tank_LiveData")  // Specify actual table name (with underscore)
                .HasKey(t => t.Tank_Number);

            // TankLiveData.Product_ID now stores Product_Code (string), not GUID
            // No conversion needed - database already stores as string

            // ✅ NEW: Configure TankLiveDataTMS primary key and table name
            // CLEAN version for TMS only (no legacy columns)
            builder.Entity<TankLiveDataTMS>()
                .ToTable("Tank_LiveDataTMS")
                .HasKey(t => t.Tank_Number);
        }
    }
}