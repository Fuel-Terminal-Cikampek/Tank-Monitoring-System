using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TMS.Web.Areas.Identity.Data;
using TMS.Web.Models;

namespace TMS.Web.Services
{
    /// <summary>
    /// Custom UserClaimsPrincipalFactory for legacy ASP.NET 2.0 Membership
    /// Does NOT load user claims from database (AspNetUserClaims table doesn't exist)
    /// Only creates basic ClaimsPrincipal with user ID and username
    /// </summary>
    public class LegacyUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, AppRole>
    {
        public LegacyUserClaimsPrincipalFactory(
            UserManager<AppUser> userManager,
            RoleManager<AppRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, roleManager, optionsAccessor)
        {
        }

        /// <summary>
        /// Generate ClaimsPrincipal for legacy user without loading claims from database
        /// </summary>
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            // Create basic claims identity without querying database for claims
            var userId = await UserManager.GetUserIdAsync(user);
            var userName = await UserManager.GetUserNameAsync(user);

            var id = new ClaimsIdentity("Identity.Application",
                Options.ClaimsIdentity.UserNameClaimType,
                Options.ClaimsIdentity.RoleClaimType);

            id.AddClaim(new Claim(Options.ClaimsIdentity.UserIdClaimType, userId));
            id.AddClaim(new Claim(Options.ClaimsIdentity.UserNameClaimType, userName));

            // Add security stamp claim
            if (UserManager.SupportsUserSecurityStamp)
            {
                var securityStamp = await UserManager.GetSecurityStampAsync(user);
                if (!string.IsNullOrEmpty(securityStamp))
                {
                    id.AddClaim(new Claim(Options.ClaimsIdentity.SecurityStampClaimType, securityStamp));
                }
            }

            // Add roles WITHOUT querying database for role claims
            // Legacy aspnet_UsersInRoles only has UserId and RoleId mapping
            if (UserManager.SupportsUserRole)
            {
                var roles = await UserManager.GetRolesAsync(user);
                foreach (var roleName in roles)
                {
                    id.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, roleName));
                }
            }

            // DO NOT call UserManager.GetClaimsAsync(user) - this would query AspNetUserClaims table
            // Legacy database doesn't have user claims concept

            return id;
        }
    }
}
