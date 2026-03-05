using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.AutoPI
{
    public class AutoPIConfiguration
    {
        public bool PostingStatus { get; set; }
        public string UrlPosting { get; set; }
        public string PlantCode { get; set; }
        public string TimeStart { get; set; }
        public string TimeEnd { get; set; }
        public string TimeFilter1 { get; set; }
        public string TimeFilter2 { get; set; }
        public string TimeFilter3 { get; set; }
        public string TimeFilter4 { get; set; }
    }
}
