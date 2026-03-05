using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Web.Areas.Identity.Data
{
    [Table("aspnet_Roles")]
    public class AspnetRoles
    {
        [Key]
        [Column("RoleId")]
        public Guid RoleId { get; set; }

        [Required]
        [Column("ApplicationId")]
        public Guid ApplicationId { get; set; }

        [Required]
        [Column("RoleName")]
        [StringLength(256)]
        public string RoleName { get; set; }

        [Required]
        [Column("LoweredRoleName")]
        [StringLength(256)]
        public string LoweredRoleName { get; set; }

        [Column("Description")]
        [StringLength(256)]
        public string? Description { get; set; }

        [Column("Create_Time")]
        public DateTime? Create_Time { get; set; }

        [Column("Create_By")]
        [StringLength(100)]
        public string? Create_By { get; set; }

        [Column("Update_Time")]
        public DateTime? Update_Time { get; set; }

        [Column("Update_By")]
        [StringLength(100)]
        public string? Update_By { get; set; }

        // Navigation Properties
        public virtual ICollection<AspnetUserInRoles>? UsersInRoles { get; set; }
    }
}
