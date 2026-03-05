using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TMS.Models;

namespace TMS.ATGService
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Master_Product { get; set; }
        public DbSet<Tank> Tank { get; set; }
        public DbSet<TankLiveData> Tank_Live_Data { get; set; }
        public DbSet<TankHistorical> Tank_Historical { get; set; }
        public DbSet<TankMovement> Tank_Movement { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ FIX: Konfigurasi relasi one-to-one Tank - TankMovement
            modelBuilder.Entity<Tank>()
                .HasOne(t => t.tankMovement)
                .WithOne(tm => tm.tank)
                .HasForeignKey<TankMovement>(tm => tm.Tank_Number) // TankMovement adalah dependent
                .HasPrincipalKey<Tank>(t => t.Tank_Name); // Tank adalah principal

            // ✅ FIX: Konfigurasi relasi one-to-one Tank - TankLiveData
            modelBuilder.Entity<Tank>()
                .HasOne(t => t.tankLiveData)
                .WithOne(tl => tl.tank)
                .HasForeignKey<TankLiveData>(tl => tl.Tank_Number) // TankLiveData adalah dependent
                .HasPrincipalKey<Tank>(t => t.Tank_Name); // Tank adalah principal

            // ✅ Konfigurasi Primary Key untuk Tank
            modelBuilder.Entity<Tank>()
                .HasKey(t => t.Tank_Name);

            // ✅ Konfigurasi Primary Key untuk TankMovement
            modelBuilder.Entity<TankMovement>()
                .HasKey(tm => tm.Tank_Number);

            // ✅ Konfigurasi Primary Key untuk TankLiveData
            modelBuilder.Entity<TankLiveData>()
                .HasKey(tl => tl.Tank_Number);

            // ✅ Konfigurasi Primary Key untuk TankHistorical
            modelBuilder.Entity<TankHistorical>()
                .HasKey(th => th.Id);

            // ✅ Konfigurasi Primary Key untuk Product
            modelBuilder.Entity<Product>()
                .HasKey(p => p.Product_Code);
        }
    }
}