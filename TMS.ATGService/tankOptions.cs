using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.ATGService
{
    public class tankConfiguration
    {
        public string tankIp { get; set; }
        public int tankPort { get; set; }
        public int LengthRegister { get; set; }
        public int timeInterval { get; set; }
        public int cntFlowrate { get; set; }
        public int FlowrateHoldSeconds { get; set; } = 12; // Default 12 detik
        public List<TankDetail> tankDetail { get; set; }
    }
    public class TankDetail
    {
        public int TankId { get; set; }
        public string TankName { get; set; }
        public int startAddr { get; set; }
        public int stopAddr { get; set; }
    }
}
