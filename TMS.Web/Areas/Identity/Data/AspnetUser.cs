using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Web.Areas.Identity.Data
{
    [Table("aspnet_Users")]
    public class AspnetUser
    {
        [Key]
        [Column("UserId")]
        public Guid UserId { get; set; }

        [Required]
        [Column("ApplicationId")]
        public Guid ApplicationId { get; set; }

        [Required]
        [Column("UserName")]
        [StringLength(256)]
        public string UserName { get; set; }

        [Required]
        [Column("LoweredUserName")]
        [StringLength(256)]
        public string LoweredUserName { get; set; }

        [Column("MobileAlias")]
        [StringLength(16)]
        public string? MobileAlias { get; set; }

        [Required]
        [Column("IsAnonymous")]
        public bool IsAnonymous { get; set; }

        [Required]
        [Column("LastActivityDate")]
        public DateTime LastActivityDate { get; set; }

        // Navigation Properties
        public virtual AspnetMembership? AspnetMembership { get; set; }
        public virtual ICollection<AspnetUserInRoles>? UsersInRoles { get; set; }
    }
}
