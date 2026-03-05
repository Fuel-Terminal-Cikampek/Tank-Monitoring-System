using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CSL.Web.Models;

namespace TMS.Web.Areas.Identity.Data
{
    // Add profile data for application users by adding properties to the AppUser class
    // Use IdentityUser<Guid> because database aspnet_Users has UserId as uniqueidentifier (GUID)
    public class AppUser : IdentityUser<Guid>
    {
        // Override PasswordHash to return dummy value for legacy users
        // Real password verification happens in LegacyPasswordHasher via aspnet_Membership table
        private string _passwordHash;
        [NotMapped]
        public override string PasswordHash
        {
            get => _passwordHash ?? "LEGACY_PASSWORD_PLACEHOLDER";
            set => _passwordHash = value;
        }

        // Override SecurityStamp to return dummy value for legacy users
        // Legacy aspnet_Users table doesn't have SecurityStamp column
        private string _securityStamp;
        [NotMapped]
        public override string SecurityStamp
        {
            get => _securityStamp ?? Guid.NewGuid().ToString();
            set => _securityStamp = value;
        }

        [PersonalData]
        [Column(TypeName = "varchar(100)")]
        [Required(ErrorMessage = "Nama lengkap wajib diisi")]
        [Display(Name = "Nama Lengkap")]
        public string FullName { get; set; }

        [PersonalData]
        [Display(Name = "Upload Foto")]
        public byte[]? UserPhoto { get; set; }

        // ✅ Required columns from aspnet_Users table
        [Column("ApplicationId")]
        public Guid ApplicationId { get; set; } = Guid.Parse("7C2BCA7E-2A59-40EF-97F1-AAD55EE4725D"); // FDM ApplicationId from aspnet_Applications

        [Column("IsAnonymous")]
        public bool IsAnonymous { get; set; } = false; // false will be stored as 0 in database

        [Column("LastActivityDate")]
        public DateTime LastActivityDate { get; set; } = DateTime.Now;
    }
}
