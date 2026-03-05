namespace TMS.Web.Authorization
{
    /// <summary>
    /// Role constants untuk authorization
    /// </summary>
    public static class RoleConstants
    {
        // ========================================
        // ADMINISTRATOR LEVEL ROLES (Full Access)
        // ========================================
        public const string Administrator = "Administrator";
        public const string Developer = "Developer";
        public const string ShiftSupervisor = "Shift Supervisor";

        /// <summary>
        /// Comma-separated list of admin roles for [Authorize(Roles = ...)]
        /// </summary>
        public const string AdminRoles = Administrator + "," + Developer + "," + ShiftSupervisor;

        // ========================================
        // OTHER ROLES (Restricted Access)
        // ========================================
        public const string Dispatch = "Dispatch";
        public const string DispatchOperator = "Dispatch Operator";
        public const string Guest = "Guest";
        public const string HSE = "HSE";
        public const string OperationHead = "Operation Head";
        public const string Operator = "Operator";
        public const string Owner = "Owner";
        public const string QQ = "QQ";
        public const string SDRegion = "S&D Region";
        public const string SalesService = "Sales Service";
        public const string SeniorSupervisor = "Senior Supervisor";
        public const string SpvMSHSE = "Spv MS HSE";
        public const string SpvPN = "Spv PN";
        public const string SpvRS = "Spv RS";
        public const string SrSpvRSD = "Sr Spv RSD";
        public const string Supervisor = "Supervisor";

        // ========================================
        // HELPER METHODS
        // ========================================

        /// <summary>
        /// Check if a role is an administrator-level role
        /// </summary>
        public static bool IsAdminRole(string roleName)
        {
            if (string.IsNullOrEmpty(roleName))
                return false;

            return roleName == Administrator ||
                   roleName == Developer ||
                   roleName == ShiftSupervisor;
        }

        /// <summary>
        /// Get array of admin role names
        /// </summary>
        public static string[] GetAdminRoles()
        {
            return new[] { Administrator, Developer, ShiftSupervisor };
        }
    }
}
