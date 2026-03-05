using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Models
{
    /// <summary>
    /// Tank Ticket Auto CHK - Automatic stock checking records
    /// </summary>
    [Table("Tank_Ticket_AutoCHK")]
    public class TankTicketAutoCHK
    {
        [Key]
        [Column("Tank_Ticket_ID")]
        public long Id { get; set; }

        [Column("Tank_Number")]
        [StringLength(50)]
        public string? Tank_Number { get; set; }

        [Column("Status")]
        public int Operation_Status { get; set; }

        [Column("TimeStamps")]
        public DateTime? Timestamp { get; set; }

        [Column("Level")]
        public int? Level { get; set; }

        [Column("Temperature")]
        public float? Temperature { get; set; }

        [Column("Density")]
        public float? Density { get; set; }

        [Column("Level_Water")]
        public int? Level_Water { get; set; }

        [Column("Tank_Ticket_No")]
        public string? Ticket_Number { get; set; }

        [Column("Shipment_ID")]
        public string? Shipment_Id { get; set; }

        [Column("Volume_Obs")]
        public float? Volume_Obs { get; set; }

        // NotMapped property to convert Operation_Status to text
        [NotMapped]
        public string Operation_Type
        {
            get
            {
                return Operation_Status switch
                {
                    1 => "OPEN",
                    2 => "CLOSE",
                    _ => "UNKNOWN"
                };
            }
        }
    }
}
