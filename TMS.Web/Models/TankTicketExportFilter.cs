using System;

namespace TMS.Web.Models
{
    public class TankTicketExportFilter
    {
        public string? TankName { get; set; }
        public string? OperationType { get; set; }
        public int? OperationStatus { get; set; }
        public string? TicketNumber { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}
