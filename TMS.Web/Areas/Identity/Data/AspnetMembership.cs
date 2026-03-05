using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Web.Areas.Identity.Data
{
    [Table("aspnet_Membership")]
    public class AspnetMembership
    {
        [Key]
        public Guid UserId { get; set; }

        public Guid ApplicationId { get; set; }

        [Required]
        [StringLength(128)]
        public string Password { get; set; }

        [Required]
        public int PasswordFormat { get; set; }

        [StringLength(128)]
        public string? PasswordSalt { get; set; }

        [StringLength(16)]
        public string? MobilePIN { get; set; }

        [StringLength(256)]
        public string? Email { get; set; }

        [StringLength(256)]
        public string? LoweredEmail { get; set; }

        [StringLength(256)]
        public string? PasswordQuestion { get; set; }

        [StringLength(128)]
        public string? PasswordAnswer { get; set; }

        public bool IsApproved { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime LastLoginDate { get; set; }
        public DateTime LastPasswordChangedDate { get; set; }
        public DateTime LastLockoutDate { get; set; }
        public int FailedPasswordAttemptCount { get; set; }
        public DateTime FailedPasswordAttemptWindowStart { get; set; }
        public int FailedPasswordAnswerAttemptCount { get; set; }
        public DateTime FailedPasswordAnswerAttemptWindowStart { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        // Foreign Key
        [ForeignKey("UserId")]
        public virtual AspnetUser User { get; set; }
    }
}
