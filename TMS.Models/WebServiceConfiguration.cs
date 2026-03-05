using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.Models
{
    [Table("Service_Configuration")]
    public class WebServiceConfiguration
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("service_name")]
        [Display(Name = "SERVICE NAME")]
        public string? ServiceName { get; set; }

        [Column("status")]
        [Display(Name = "STATUS")]
        public bool Status { get; set; }

        [Column("url")]
        [Display(Name = "URL")]
        public string? URL { get; set; }

        [Column("username")]
        [Display(Name = "USERNAME")]
        public string? Username { get; set; }

        [Column("password")]
        [Display(Name = "PASSWORD")]
        public string? Password { get; set; }

        [Column("create_time")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Create")]
        public DateTime? CreatedTimeStamp { get; set; }

        [Column("create_by")]
        public string? CreateBy { get; set; }

        [Column("update_time")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Update")]
        public DateTime? UpdatedTimeStamp { get; set; }

        [Column("update_by")]
        public string? UpdateBy { get; set; }

    }
}

