using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.Models
{
    public class RequestEstimated
    {
        [DisplayName("TANK")]
        public string Tank_Name { get; set; }

        // Backward compatibility - TankId no longer used
        [DisplayName("TANK")]
        public int TankId { get; set; }
        [DisplayName("SAFE HEIGHT")]
        public double Height { get; set; }
        [DisplayName("STATUS")]
        public int Status { get; set; }
        [DisplayName("TIME ALARM")]
        public TimeSpan Time { get; set; }
    }
}
