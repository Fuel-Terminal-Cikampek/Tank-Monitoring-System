using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Models
{
    [Table("Tank_Ticket")]
    public class TankTicket
    {
        [Key]
        [Column("Tank_Ticket_ID")]
        public long Id { get; set; }

        [Column("Tank_Ticket_No")]
        [Display(Name = "TICKET NUMBER")]
        [StringLength(100)]
        public string? Ticket_Number { get; set; }

        [Display(Name = "DATE AND TIME")]
        [Column("TimeStamps")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd MMM yyyy hh:mm}", ApplyFormatInEditMode = true)]
        public DateTime? Timestamp { get; set; }

        [Column("Delivery_Order_ID")]
        [Display(Name = "DO NUMBER")]
        [StringLength(50)]
        public string? Do_Number { get; set; }

        [Column("Shipment_ID")]
        [Display(Name = "SHIPMENT")]
        [StringLength(100)]
        public string? Shipment_Id { get; set; }

        [ForeignKey("TankLiveData")]
        [Display(Name = "TANK")]
        [StringLength(50)]
        public string? Tank_Number { get; set; }

        // Backward compatibility - Tank_Id no longer used as FK
        [NotMapped]
        [Display(Name = "TANK ID")]
        public int Tank_Id { get; set; }

        [Column("Status")]
        [Display(Name = "STATUS")]
        public int Operation_Status { get; set; }

        [NotMapped]
        [Display(Name = "OPERATION TYPE")]
        public string Operation_Type
        {
            get
            {
                // ✅ FIX: Map Status_Reservasi to Operation_Type dengan deskripsi lengkap
                // Format: (KODE) DESKRIPSI - sesuai kode referensi
                return StatusReservasi switch
                {
                    1 => "PSC",                                    // PSC - tidak di-post ke SAP
                    2 => "Routine Dipping",                        // Routine Dipping - tidak di-post ke SAP
                    3 => "(TT) TANK TO TANK TRANSFER",            // Tank to Tank Transfer
                    4 => "(ROT) RECEIPT OTHERS",                  // Receiving Others
                    5 => "(ILS) ISSUE TO LSTK",                   // Issue to LSTK/Sales
                    6 => "(PI) PHYSICAL INVENTORY",               // Physical Inventory
                    7 => "(CHK) STOCK CHECKING",                  // Stock Checking
                    8 => "(UDG) UP/DOWN GRADATION",               // Upgrade/Downgrade
                    9 => "(BLD) BLENDING",                        // Blending
                    _ => "UNKNOWN"
                };
            }
            set
            {
                // ✅ FIX: Map Operation_Type string to Status_Reservasi
                // Support both short code and full description
                var upperValue = value?.ToUpper();

                // Check if contains code in parentheses
                if (upperValue != null && upperValue.Contains("(") && upperValue.Contains(")"))
                {
                    // Extract code from "(CODE) DESCRIPTION"
                    var startIndex = upperValue.IndexOf('(') + 1;
                    var endIndex = upperValue.IndexOf(')');
                    if (endIndex > startIndex)
                    {
                        upperValue = upperValue.Substring(startIndex, endIndex - startIndex).Trim();
                    }
                }

                StatusReservasi = upperValue switch
                {
                    "PSC" => 1,
                    "RD" or "ROUTINE DIPPING" => 2,
                    "TT" => 3,
                    "ROT" => 4,
                    "ILS" => 5,
                    "PI" => 6,
                    "CHK" => 7,
                    "UDG" => 8,
                    "BLD" => 9,
                    // Support additional operation types from old system
                    "ICR" => 5,  // Map ICR (Issue Crude to Production) to ILS
                    "IS" => 5,   // Map IS (Issue to Sales) to ILS
                    "RCR" => 4,  // Map RCR (Crude Receipt) to ROT
                    "RPR" => 4,  // Map RPR (Production Receipt) to ROT
                    _ => null
                };
            }
        }

        [NotMapped]
        [Display(Name = "MEASUREMENT METHOD")]
        public string Measurement_Method { get; set; } = "ATG";

        [Column("ReceivingCQP_ID")]
        public int? ReceivingCQPID { get; set; }

        [Column("Level")]
        [DisplayName("LEVEL")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public int? LiquidLevel { get; set; }

        [Column("Level_Water")]
        [DisplayName("WATER LEVEL")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public int? WaterLevel { get; set; }

        [Column("Temperature")]
        [DisplayName("MAT. TEMP")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? LiquidTemperature { get; set; }

        [NotMapped]
        [DisplayName("TEST. TEMP")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double TestTemperature { get; set; }

        [Column("Density")]
        [DisplayName("DENSITY")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? LiquidDensity { get; set; }

        [Column("Density_15C")]
        public double? Density15C { get; set; }

        [Column("Volume_Product")]
        [DisplayName("VOLUME")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Volume { get; set; }

        [Column("Corr_Factor")]
        public double? CorrFactor { get; set; }

        [Column("Volume_15C")]
        public double? Volume15C { get; set; }

        [Column("LongTon_Factor")]
        public double? LongTonFactor { get; set; }

        [Column("Volume_LongTon")]
        public double? VolumeLongTon { get; set; }

        [Column("Barrel60F_Factor")]
        public double? Barrel60FFactor { get; set; }

        [Column("Volume_Barrel60F")]
        public double? VolumeBarrel60F { get; set; }

        [Column("Status_Ukur")]
        public int? StatusUkur { get; set; }

        [NotMapped]
        [DisplayName("PUMPABLE (L)")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double Pumpable { get; set; }

        [NotMapped]
        [DisplayName("ULLAGE (L)")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double Ullage { get; set; }

        [NotMapped]
        [DisplayName("mm/s")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double FlowRateMperSecond { get; set; }

        [NotMapped]
        [DisplayName("KL/H")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double FlowRateKLperH { get; set; }

        [Column("Status_Reservasi")]
        public int? StatusReservasi { get; set; }

        public bool? IsPrinted { get; set; }

        [Column("Volume_Obs")]
        public double? VolumeObs { get; set; }

        [Column("Level_DeadStock")]
        public int? LevelDeadStock { get; set; }

        [Column("Volume_DeadStock")]
        public double? VolumeDeadStock { get; set; }

        [Column("Level_Safe_Capacity")]
        public int? LevelSafeCapacity { get; set; }

        [Column("Volume_Safe_Capacity")]
        public double? VolumeSafeCapacity { get; set; }

        [Column("Is_Synchronized")]
        public bool? Is_Upload_Success { get; set; }

        [Column("sap_response")]
        [Display(Name = "SAP MESSAGE")]
        public string? SAP_Response { get; set; }

        [Column("Create_By")]
        [Display(Name = "Create By")]
        public string? Created_By { get; set; }

        [Column("Create_Time")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Create")]
        public DateTime? Created_Timestamp { get; set; }

        [Column("Update_By")]
        [Display(Name = "Update By")]
        public string? Updated_By { get; set; }

        [Column("Update_Time")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Update")]
        public DateTime? Updated_Timestamp { get; set; }

        // ❌ DISABLED: Alarm Status & ACK Time - temporarily disabled (columns don't exist in database)
        [NotMapped]
        // [Column("Alarm_Status")]
        [Display(Name = "Alarm Status")]
        public int? AlarmStatus { get; set; } = 1;

        [NotMapped]
        // [Column("Alarm_Ack_Time")]
        [Display(Name = "Alarm ACK Time")]
        public DateTime? AlarmAckTime { get; set; }

        public Tank? tank { get; set; }
        public TankLiveData? TankLiveData { get; set; }
    }
}
