using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.Models
{
    public class NewTankTicket
    {
        [Required(ErrorMessage = "Tank Number is required")]
        [Display(Name = "TANK. NO")]
        public string Tank_Number { get; set; }

        // Backward compatibility - Tank_Id no longer used
        [Display(Name = "TANK. NO")]
        public int Tank_Id { get; set; }

        // ✅ TAMBAHKAN PROPERTY INI
        public Tank Tank { get; set; }
        
        [Display(Name = "SHIPMENT")]
        public string Shipment_Id { get; set; }
        
        [Required(ErrorMessage = "Operation Type is required")]
        [Display(Name = "OPERATION TYPE")]
        public string Operation_Type { get; set; }
        
        public string Do_Number { get; set; }
        public string Ticket_Number { get; set; }
        public string IsPosting { get; set; }
        
        // OPEN data
        [Display(Name = "DATE")]
        public string DateOpen { get; set; }
        
        [Display(Name = "TEMP")]
        public string TimeOpen { get; set; }
        
        [Display(Name = "LEVEL")]
        public string LiquidLevelOpen { get; set; }
        
        [Display(Name = "WATER LEVEL")]
        public string WaterLevelOpen { get; set; }
        
        [Display(Name = "MAT TEMP")]
        public string LiquidTemperatureOpen { get; set; }
        
        [Display(Name = "TEST TEMP")]
        public string TestTemperatureOpen { get; set; }
        
        [Display(Name = "DENSITY")]
        public string LiquidDensityOpen { get; set; }
        
        [Display(Name = "STATUS")]
        public string CheckStatusOpen { get; set; }  // "1" or "2"
        
        // CLOSE data
        [Display(Name = "DATE")]
        public string DateClose { get; set; }
        
        [Display(Name = "TIME")]
        public string TimeClose { get; set; }
        
        [Display(Name = "LEVEL")]
        public string LiquidLevelClose { get; set; }
        
        [Display(Name = "WATER LEVEL")]
        public string WaterLevelClose { get; set; }
        
        [Display(Name = "MAT TEMP")]
        public string LiquidTemperatureClose { get; set; }
        
        [Display(Name = "TEST TEMP")]
        public string TestTemperatureClose { get; set; }
        
        [Display(Name = "DENSITY")]
        public string LiquidDensityClose { get; set; }
        
        [Display(Name = "Ticket Number")]
        public string CheckStatusClose { get; set; }  // "1" or "2"
    }
}
