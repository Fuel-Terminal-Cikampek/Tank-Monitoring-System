using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Web.Areas.Identity.Data
{
    [Table("aspnet_UsersInRoles")]
    public class AspnetUserInRoles
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }

        // Foreign Keys
        [ForeignKey("UserId")]
        public virtual AspnetUser User { get; set; }

        [ForeignKey("RoleId")]
        public virtual AspnetRoles Role { get; set; }
    }
}
