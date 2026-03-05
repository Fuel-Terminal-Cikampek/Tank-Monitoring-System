using System.ComponentModel.DataAnnotations;

namespace TMS.Web.Models
{
    /// <summary>
    /// View Model untuk Login form
    /// Digunakan untuk membership authentication
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username harus diisi")]
        [Display(Name = "Username")]
        [StringLength(256)]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password harus diisi")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [StringLength(128)]
        public string Password { get; set; }

        [Display(Name = "Ingat saya")]
        public bool RememberMe { get; set; }
    }
}
