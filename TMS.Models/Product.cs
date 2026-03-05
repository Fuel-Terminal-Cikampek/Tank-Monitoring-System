using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Models
{
    [Table("Master_Product")]
    public class Product
    {
        [Key]
        public Guid Product_ID { get; set; }
        [Required]
        [StringLength(20)]
        [Display(Name = "CODE")]
        public string Product_Code { get; set; }
        [Required]
        [StringLength(50)]
        [Display(Name = "PRODUCT")]
        public string Product_Name { get; set; }
        [Column("HexColor")]
        [Display(Name = "HEX COLOR")]
        [StringLength(10)]
        public string? HexColor { get; set; }
        [Display(Name = "DEFAULT DENSITY  (kg/m&sup3;)")]
        public double Default_Density { get; set; }

        [Display(Name = "DEFAULT TEMP (&deg;C)")]
        public double Default_Temp { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "DATE CREATED ")]
        public DateTime? Create_Time { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "DATE UPDATED")]
        public DateTime? Update_Time { get; set; }
        [Display(Name = "CREATE BY")]
        public string? Create_By { get; set; }
        [Display(Name = "UPDATE BY")]
        public string? Update_By { get; set; }
        // Navigation to Tank removed - join manually in code due to type mismatch (int vs string Product_ID)
    }
}
