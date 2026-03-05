using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.Models
{
    public class EditTankTicket
    {
        public long Ticket_ID { get; set; }
        [Display(Name = "TANK. NO")]
        public int Tank_Id { get; set; }
        [Display(Name = "SHIPMENT")]
        public string Shipment_Id { get; set; }
        [Display(Name = "OPERATION TYPE")]
        public string Operation_Type { get; set; }
        [Display(Name = "Status")]
        public int Operation_Status { get; set; }
        [Display(Name = "DO NUMBER")]
        public string Do_Number { get; set; }
        [Display(Name = "TICKET NUMBER")]
        public string Ticket_Number { get; set; }
        public string IsPosting { get; set; }

        [Display(Name = "Ticket Number")]
        public Tank Tank { get; set; }
        //model open
        [Display(Name = "DATE")]
        public string Date { get; set; }
        [Display(Name = "TEMP")]
        public string Time { get; set; }
        [Display(Name = "LEVEL")]
        public string LiquidLevel { get; set; }
        [Display(Name = "WATER LEVEL")]
        public string WaterLevel { get; set; }
        [Display(Name = "MAT TEMP")]
        public string LiquidTemperature { get; set; }
        [Display(Name = "TEST TEMP")]
        public string TestTemperature { get; set; }
        [Display(Name = "DENSITY")]

        public string LiquidDensity { get; set; }
    }
}
