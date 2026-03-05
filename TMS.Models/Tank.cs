using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using TMS.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;

namespace TMS.Models
{
    public class Tank
    {
        [Key]
        [StringLength(20)]
        [DisplayName("TANK")]
        public string Tank_Name { get; set; }

        [Column("Product_ID")]
        [StringLength(20)]
        public string Product_ID { get; set; }  // Store Product_Code (string), not GUID

        // Backward compatibility - TankId no longer exists in TAS_CIKAMPEK2014
        // Use Tank_Name as primary key instead
        [NotMapped]
        public int TankId { get; set; }

        // Database columns that exist in TAS_CIKAMPEK2014
        [Column("Group_Name")]
        [StringLength(255)]
        public string? Group_Name { get; set; }  // Nullable in database

        [Column("Tank_Height")]
        [DisplayName("TANK HIGHT ")]
        public double Tank_Height { get; set; }

        [Column("Tank_Diameter")]
        [DisplayName("TANK DIAMETER")]
        public double Tank_Diameter { get; set; }

        [Column("Tank_Form")]
        [StringLength(50)]
        public string? Tank_Form { get; set; }  // Nullable in database

        [Column("Height_Safe_Capacity")]
        [DisplayName("SAFE LEVEL")]
        public int Height_Safe_Capacity { get; set; }  // INT in database, not double!

        [Column("Height_Vol_Max")]
        public int Height_Vol_Max { get; set; }

        [Column("Height_Point_Desk")]
        public int Height_Point_Desk { get; set; }

        [Column("Height_Tank_Base")]
        public int Height_Tank_Base { get; set; }

        [Column("Stretch_Coefficient")]
        public double Stretch_Coefficient { get; set; }

        [Column("Density_Calibrate")]
        public double Density_Calibrate { get; set; }

        [Column("RaisePerMM")]
        public double RaisePerMM { get; set; }

        [Column("IsUsed")]
        [DisplayName("iS USED?")]
        public bool? IsUsed { get; set; }  // Nullable in database (YES)

        [Column("IsAutoPI")]
        [DisplayName("IS AUTO PI?")]
        public bool? IsAutoPI { get; set; }  // Nullable in database (YES)

        [Column("Create_Time")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "DATE CREATE")]
        public DateTime? Create_Time { get; set; }

        [Column("Create_By")]
        [Display(Name = "CREATE BY")]
        public string? Create_By { get; set; }  // Nullable - can be NULL in database

        [Column("Update_Time")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "DATE UPDATE")]
        public DateTime? Update_Time { get; set; }

        [Column("Update_By")]
        [Display(Name = "UPDATE BY")]
        public string? Update_By { get; set; }  // Nullable - can be NULL in database

        [Column("Deadstock_Volume")]
        public double? Deadstock_Volume { get; set; }  // Nullable in database (YES)

        // ========== PRIORITY 2 - Now mapped to database columns ==========
        // TankVolume is a computed property that points to Height_Vol_Max (already in Liters)
        [NotMapped]
        [DisplayName("TANK VOLUME")]
        public double? TankVolume
        {
            get => Height_Vol_Max;
            set => Height_Vol_Max = value.HasValue ? (int)value.Value : 0;
        }

        [Column("IsManualTemp")]
        [DisplayName("iS MANUAL TEMP?")]
        public bool? IsManualTemp { get; set; }

        [Column("ManualTemp")]
        [DisplayName("MANUAL TEMP")]
        public double? ManualTemp { get; set; }

        [Column("IsManualTestTemp")]
        [DisplayName("Is MANUAL TEST TEMP?")]
        public bool? IsManualTestTemp { get; set; }

        [Column("IsManualDensity")]
        [DisplayName("Is MANUAL DENSITY?")]
        public bool? IsManualDensity { get; set; }

        [Column("IsManualWaterLevel")]
        [DisplayName("Is MANUAL WATERLEVEL?")]
        public bool? IsManualWaterLevel { get; set; }

        [Column("ManualWaterLevel")]
        [DisplayName("MANUAL WATERLEVEL")]
        public double? ManualWaterLevel { get; set; }

        [Column("ManualTestTemp")]
        [DisplayName("TEST TEMP")]
        public double? ManualTestTemp { get; set; }

        [Column("ManualLabDensity")]
        [DisplayName("MANUAL DENSITY")]
        public double? ManualLabDensity { get; set; }

        [Column("IsManualLevel")]
        [DisplayName("iS MANUAL LEVEL?")]
        public bool? IsManualLevel { get; set; }

        [Column("ManualLevel")]
        [DisplayName("MANUAL Level")]
        public double? ManualLevel { get; set; }

        [Column("DefaultKLperH")]
        public double? DefaultKLperH { get; set; }

        // ========== Volume & Tank Measurement ==========
        // VolumeSafeCapacity -> mapping ke Height_Safe_Capacity (tidak perlu kolom baru)
        [NotMapped]
        [DisplayName("SAFE VOLUME")]
        public double VolumeSafeCapacity
        {
            get => Height_Safe_Capacity;
            set => Height_Safe_Capacity = (int)value;
        }

        // Height_Deadstock -> Maps directly to Height_Dead_Stock column in database
        [Column("Height_Dead_Stock")]
        [DisplayName("DEADSTOCK")]
        public int Height_Deadstock { get; set; }

        [Column("Tape")]
        [DisplayName("TAPE")]
        public double? Tape { get; set; }

        [Column("Bob")]
        [DisplayName("BOB")]
        public double? Bob { get; set; }

        [Column("Blank")]
        [DisplayName("BLANK")]
        public double? Blank { get; set; }

        // ========== Alarm Level Configuration ==========
        [Column("UseAlarmLL")]
        [DisplayName("USE ALARM LL")]
        public bool? UseAlarmLL { get; set; }

        [Column("LevelLL")]
        [DisplayName("LEVEL LL")]
        public double? LevelLL { get; set; }

        [Column("UseAlarmL")]
        [DisplayName("USE ALARM L")]
        public bool? UseAlarmL { get; set; }

        [Column("LevelL")]
        [DisplayName("LEVEL L")]
        public double? LevelL { get; set; }

        [Column("UseAlarmH")]
        [DisplayName("USE ALARM H")]
        public bool? UseAlarmH { get; set; }

        [Column("LevelH")]
        [DisplayName("LEVEL H")]
        public double? LevelH { get; set; }

        [Column("UseAlarmHH")]
        [DisplayName("USE ALARM HH")]
        public bool? UseAlarmHH { get; set; }

        [Column("LevelHH")]
        [DisplayName("LEVEL HH")]
        public double? LevelHH { get; set; }

        // ========== Alarm Temperature Configuration ==========
        [Column("UseAlarmTempL")]
        [DisplayName("USE ALARMTEMP L")]
        public bool? UseAlarmTempL { get; set; }

        [Column("TempL")]
        [DisplayName("TEMP L")]
        public double? TempL { get; set; }

        [Column("UseAlarmTempH")]
        [DisplayName("USE ALARMTEMP H")]
        public bool? UseAlarmTempH { get; set; }

        [Column("TempH")]
        [DisplayName("TEMP H")]
        public double? TempH { get; set; }

        // Navigation to TankLiveData - manually assigned in controller (not EF navigation)
        [NotMapped]
        public TankLiveData tankLiveData { get; set; }

        // ✅ UPDATED: NotMapped because Tank_Movement now uses Tank_Movement_ID as PK (not Tank_Name)
        // One Tank can have multiple Tank_Movement records - query manually in controller
        [NotMapped]
        public TankMovement tankMovement { get; set; }

        // Hybrid flowrate - calculated in controller (manual < 6 min, sensor >= 6 min)
        [NotMapped]
        public double HybridFlowrate { get; set; }

        // Navigation to Product removed - join manually in code due to type mismatch (string vs int)
        // Use: tanks.Join(products, t => t.Product_ID.ToString(), p => p.Product_ID.ToString(), ...)

        public ICollection<TankHistorical> TankHistoricals { get; set; }
        public ICollection<TankTicket> TankTickets { get; set; }
    }
}
