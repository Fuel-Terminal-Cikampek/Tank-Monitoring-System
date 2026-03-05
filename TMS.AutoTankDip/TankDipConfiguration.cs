using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.TankDipPosting
{
    public class TankDipConfiguration
    {
        public string UrlPosting { get; set; }
        public string PlantCode { get; set; }
    }
    public class IntervalPosting
    {
        public int TimeInterval { get; set; }
    }
}
