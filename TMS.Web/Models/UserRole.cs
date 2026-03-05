using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using TMS.Web.Areas.Identity.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace CSL.Web.Models
{
    public class UserRole : IdentityUserRole<Guid>
    {
        [Column("Create_Timestamp")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Create")]
        public DateTime? Create_Time { get; set; }

        [Column("Create_By")]
        public string? Create_By { get; set; }

        [Column("Update_Timestamp")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Update")]
        public DateTime? Update_Time { get; set; }

        [Column("Update_By")]
        public string? Update_By { get; set; }
    }
}
