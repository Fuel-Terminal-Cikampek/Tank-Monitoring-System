using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMS.Web.Areas.Identity.Data;
using CSL.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TMS.Models;
using TMS.Web.Models;

namespace TMS.Web.Areas.Identity.Data
{
    public class TMSContext : IdentityDbContext<AppUser, AppRole, Guid>
    {
        public TMSContext(DbContextOptions<TMSContext> options)
            : base(options)
        {        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // ❌ Ignore ASP.NET Core Identity tables that don't exist in legacy database
            // IMPORTANT: Must call Ignore() BEFORE base.OnModelCreating()
            builder.Ignore<IdentityUserClaim<Guid>>();
            builder.Ignore<IdentityUserLogin<Guid>>();
            builder.Ignore<IdentityUserToken<Guid>>();
            builder.Ignore<IdentityRoleClaim<Guid>>();

            base.OnModelCreating(builder);

            // ❌ Exclude legacy entities from automatic mapping
            // We use Identity framework entities instead (AppUser, AppRole, IdentityUserRole<Guid>)
            builder.Ignore<AspnetUser>();
            builder.Ignore<AspnetRoles>();
            builder.Ignore<AspnetUserInRoles>(); // Use IdentityUserRole<Guid> instead

            // ✅ Map AppUser to existing aspnet_Users table (OPTION B: No DB changes)
            builder.Entity<AppUser>()
                .ToTable("aspnet_Users")
                .HasKey(u => u.Id);

            // Map AppUser properties to existing aspnet_Users columns
            builder.Entity<AppUser>()
                .Property(u => u.Id).HasColumnName("UserId");

            builder.Entity<AppUser>()
                .Property(u => u.UserName).HasColumnName("UserName");

            builder.Entity<AppUser>()
                .Property(u => u.NormalizedUserName).HasColumnName("LoweredUserName");

            // ✅ Map required aspnet_Users columns
            builder.Entity<AppUser>()
                .Property(u => u.ApplicationId).HasColumnName("ApplicationId").IsRequired();

            builder.Entity<AppUser>()
                .Property(u => u.IsAnonymous).HasColumnName("IsAnonymous").IsRequired();

            builder.Entity<AppUser>()
                .Property(u => u.LastActivityDate).HasColumnName("LastActivityDate").IsRequired();

            // Mark Identity properties as NotMapped (not in aspnet_Users table)
            // NOTE: PasswordHash and SecurityStamp are already marked [NotMapped] in AppUser class
            builder.Entity<AppUser>()
                .Ignore(u => u.Email)
                .Ignore(u => u.NormalizedEmail)
                .Ignore(u => u.EmailConfirmed)
                .Ignore(u => u.ConcurrencyStamp)
                .Ignore(u => u.PhoneNumber)
                .Ignore(u => u.PhoneNumberConfirmed)
                .Ignore(u => u.TwoFactorEnabled)
                .Ignore(u => u.LockoutEnd)
                .Ignore(u => u.LockoutEnabled)
                .Ignore(u => u.AccessFailedCount)
                // Ignore custom AppUser properties not in aspnet_Users table
                .Ignore(u => u.FullName)
                .Ignore(u => u.UserPhoto);

            // ✅ Map AppRole to existing aspnet_Roles table
            builder.Entity<AppRole>()
                .ToTable("aspnet_Roles")
                .HasKey(r => r.Id);

            builder.Entity<AppRole>()
                .Property(r => r.Id).HasColumnName("RoleId");

            builder.Entity<AppRole>()
                .Property(r => r.Name).HasColumnName("RoleName");

            builder.Entity<AppRole>()
                .Property(r => r.NormalizedName).HasColumnName("LoweredRoleName");

            // Ignore Identity properties not in aspnet_Roles
            builder.Entity<AppRole>()
                .Ignore(r => r.ConcurrencyStamp);

            // ❌ Ignore UserRole custom class to prevent inheritance issues
            builder.Ignore<UserRole>();

            // ✅ Map IdentityUserRole<Guid> to existing aspnet_UsersInRoles table
            builder.Entity<IdentityUserRole<Guid>>()
                .ToTable("aspnet_UsersInRoles")
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            // ✅ Disable discriminator column (not needed, no inheritance in DB)
            builder.Entity<IdentityUserRole<Guid>>()
                .HasNoDiscriminator();

            // Configure Tank primary key and table name (TAS_CIKAMPEK2014 uses Tank_Name as PK)
            builder.Entity<Tank>()
                .ToTable("Tank")
                .HasKey(t => t.Tank_Name);

            // Configure Product table name
            builder.Entity<Product>()
                .ToTable("Master_Product");

            // Configure TankLiveData primary key and table name
            builder.Entity<TankLiveData>()
                .ToTable("Tank_LiveData")
                .HasKey(t => t.Tank_Number);

            // ✅ NEW: Configure TankLiveDataTMS primary key and table name
            // CLEAN version for TMS only (no legacy columns)
            builder.Entity<TankLiveDataTMS>()
                .ToTable("Tank_LiveDataTMS")
                .HasKey(t => t.Tank_Number);

            // Configure TankHistorical
            builder.Entity<TankHistorical>()
                .Property(t => t.Tank_Number)
                .HasColumnName("Tank_Number");

            // ✅ Configure TankMovement primary key and relationship with Tank
            // ✅ UPDATED: Use Tank_Movement_ID as PK (auto-increment IDENTITY)
            // Tank_Number is now just a FK, not PK anymore
            // ✅ FIX: NO VALUE CONVERTER - database uses BIT (native boolean)
            // The columns Status, StagnantAlarm, IsLevel are BIT type in SQL Server
            builder.Entity<TankMovement>()
                .ToTable("Tank_Movement")
                .HasKey(t => t.Tank_Movement_ID);

            // ✅ Explicitly map boolean columns to ensure correct type handling
            builder.Entity<TankMovement>()
                .Property(t => t.Status)
                .HasColumnName("Status")
                .HasColumnType("int");

            builder.Entity<TankMovement>()
                .Property(t => t.StagnantAlarm)
                .HasColumnName("StagnantAlarm")
                .HasColumnType("bit");

            builder.Entity<TankMovement>()
                .Property(t => t.IsLevel)
                .HasColumnName("IsLevel")
                .HasColumnType("bit");

            // ✅ FIX: Explicitly configure Tank_Number as FK to Tank.Tank_Name
            // This prevents EF Core from creating shadow property "Tank_Name1"
            builder.Entity<TankMovement>()
                .Property(t => t.Tank_Number)
                .HasColumnName("Tank_Name");

            // ✅ Configure many-to-one relationship: TankMovement -> Tank
            builder.Entity<TankMovement>()
                .HasOne(tm => tm.tank)
                .WithMany()  // One Tank can have many TankMovements
                .HasForeignKey(tm => tm.Tank_Number)
                .HasPrincipalKey(t => t.Tank_Name)
                .OnDelete(DeleteBehavior.NoAction);

            // ✅ UPDATED: Comment out one-to-one relationship (no longer valid)
            // Tank_Movement now uses Tank_Movement_ID as PK (auto-increment)
            // Multiple Tank_Movement records can exist per Tank
            // Tank_Movement.Tank_Number is still FK to Tank.Tank_Name (configured at model level)
            // builder.Entity<Tank>()
            //     .HasOne(t => t.tankMovement)
            //     .WithOne(tm => tm.tank)
            //     .HasForeignKey<TankMovement>(tm => tm.Tank_Number)
            //     .OnDelete(DeleteBehavior.Cascade);

            // Configure TankTicket relationship with Tank
            builder.Entity<TankTicket>()
                 .HasOne(b => b.tank)
                 .WithMany(a => a.TankTickets)
                 .HasForeignKey(b => b.Tank_Number);
        }

        // Identity DbSets
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<AppRole> AppRoles { get; set; }
        public DbSet<UserRole> userRoles { get; set; }

        // Legacy entities - IGNORED in OnModelCreating but kept here for backward compatibility
        public DbSet<AspnetUser> AspnetUsers { get; set; }
        public DbSet<AspnetMembership> AspnetMemberships { get; set; }
        public DbSet<AspnetRoles> AspnetRoles { get; set; }
        public DbSet<AspnetUserInRoles> AspnetUserInRoles { get; set; }

        // Business Entity DbSets
        public DbSet<Product> Master_Product { get; set; }
        public DbSet<Tank> Tank { get; set; }
        public DbSet<TankHistorical> Tank_Historical { get; set; }
        public DbSet<TankLiveData> Tank_Live_Data { get; set; }
        public DbSet<TankLiveDataTMS> Tank_LiveDataTMS { get; set; }  // ✅ NEW: Clean version for TMS only
        public DbSet<TankMovement> Tank_Movement { get; set; }
        public DbSet<TankTicket> Tank_Ticket { get; set; }
        public DbSet<TankTicketAutoCHK> Tank_Ticket_AutoCHK { get; set; }
        public DbSet<WebServiceConfiguration> WebServiceConfigurations { get; set; }

        private static Guid ParseGuidOrEmpty(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Guid.Empty;
            return Guid.TryParse(value, out var result) ? result : Guid.Empty;
        }
    }
}
