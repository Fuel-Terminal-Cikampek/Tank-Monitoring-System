using System;

namespace TMS.Web.Models
{
    public class HistoricalFilter
    {
        public string? TankNameFilter { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string TimeFilter { get; set; }
    }
}
