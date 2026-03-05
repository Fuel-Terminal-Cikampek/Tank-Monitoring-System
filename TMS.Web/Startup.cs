using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using TMS.Web.Models;
using TMS.Web.Areas.Identity.Data;
using TMS.Web.Services;
using System.Text.Json.Serialization;

namespace CSL.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<TMSContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("CTMWebContextConnection")));

            services.AddIdentity<AppUser, AppRole>(options =>
            {
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
                options.SignIn.RequireConfirmedAccount = false;
            }).AddEntityFrameworkStores<TMSContext>()
            .AddDefaultUI()
            .AddRoleManager<RoleManager<AppRole>>()
            .AddDefaultTokenProviders();

            // Register custom password hasher for legacy ASP.NET 2.0 Membership passwords
            services.AddScoped<IPasswordHasher<AppUser>, LegacyPasswordHasher>();

            // Register custom UserClaimsPrincipalFactory to avoid loading claims from non-existent AspNetUserClaims table
            services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, LegacyUserClaimsPrincipalFactory>();

            services.AddControllersWithViews();
            services.AddControllers()
                   .AddJsonOptions(options =>
                   {
                       options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                   });
            services.AddRazorPages(options => {
                options.Conventions.AllowAnonymousToPage("/Identity/Account/Login");
                options.Conventions.AllowAnonymousToPage("/Identity/Account/Logout");
            });
            services.ConfigureApplicationCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication(); //to be added
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages(); //to be added
            });
        }
    }
}
