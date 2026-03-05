using System;

namespace TMS.Web.Models
{
    public class TankTicketFilter
    {
        public string? TankNameFilter { get; set; }
        public string? OperationType { get; set; }
        public int OperationStatus { get; set; }
        public string? TicketNumber { get; set; }
        public DateTime minDateTime { get; set; }
        public DateTime maxDateTime { get; set; }
    }
}
