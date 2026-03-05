using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

namespace TMS.Web.Models
{
    // Use IdentityRole<Guid> to match AppUser<Guid> key type
    public class AppRole : IdentityRole<Guid>
    {
        public AppRole() : base() { }
        public AppRole(string roleName) : base(roleName) { }

        [StringLength(20)]
        [Display(Name = "Create By")]
        public string? Create_By { get; set; }  // Nullable - can be NULL in database

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Create Update")]
        public DateTime? Create_Time { get; set; }  // Nullable - can be NULL in database

        [StringLength(20)]
        [Display(Name = "Update By")]
        public string? Update_By { get; set; }  // Nullable - can be NULL in database

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Update")]
        public DateTime? Update_Time { get; set; }  // Nullable - can be NULL in database
    }
}

