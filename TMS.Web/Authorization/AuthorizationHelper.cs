using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TMS.Web.Authorization
{
    /// <summary>
    /// Helper methods for authorization checking
    /// </summary>
    public static class AuthorizationHelper
    {
        /// <summary>
        /// Check if current user is in an administrator-level role
        /// (Administrator, Developer, or Shift Supervisor)
        /// </summary>
        public static bool IsUserAdmin(this ClaimsPrincipal user)
        {
            if (user == null || !user.Identity.IsAuthenticated)
                return false;

            return user.IsInRole(RoleConstants.Administrator) ||
                   user.IsInRole(RoleConstants.Developer) ||
                   user.IsInRole(RoleConstants.ShiftSupervisor);
        }

        /// <summary>
        /// Check if current user has permission to Create/Update/Delete
        /// Returns true if user is admin, false otherwise
        /// </summary>
        public static bool CanModifyData(this ClaimsPrincipal user)
        {
            return user.IsUserAdmin();
        }

        /// <summary>
        /// Check if current user has permission to Export (all users can export)
        /// </summary>
        public static bool CanExport(this ClaimsPrincipal user)
        {
            // All authenticated users can export
            return user != null && user.Identity.IsAuthenticated;
        }

        /// <summary>
        /// Return JsonResult with access denied message if user is not admin
        /// Usage: if (!User.CanModifyData()) return this.AccessDeniedJson();
        /// </summary>
        public static JsonResult AccessDeniedJson(this Controller controller)
        {
            return controller.Json(new
            {
                isValid = false,
                message = "Access Denied: Only Administrator, Developer, or Shift Supervisor can perform this action."
            });
        }

        /// <summary>
        /// Return ViewResult with access denied message if user is not admin
        /// </summary>
        public static ViewResult AccessDeniedView(this Controller controller)
        {
            controller.TempData["ErrorMessage"] = "Access Denied: Only Administrator, Developer, or Shift Supervisor can perform this action.";
            return controller.View("AccessDenied");
        }
    }
}
