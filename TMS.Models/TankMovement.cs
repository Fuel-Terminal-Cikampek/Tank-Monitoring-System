using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.Models
{
    public class TankMovement
    {
        // ✅ NEW: Auto-increment Primary Key
        [Key]
        [Column("Tank_Movement_ID")]
        [DisplayName("MOVEMENT ID")]
        public int Tank_Movement_ID { get; set; }

        // ✅ UPDATED: No longer primary key, just FK to Tank
        [Column("Tank_Name")]
        [DisplayName("TANK")]
        [StringLength(20)]
        public string Tank_Number { get; set; }

        // Backward compatibility - TankId no longer used
        [NotMapped]
        public int TankId { get; set; }

        [Column("TimeStamp")]
        [DisplayName("TIMESTAMP")]
        [DataType(DataType.DateTime)]
        public DateTime? TimeStamp { get; set; }

        [Column("Level")]
        [DisplayName("LEVEL")]
        public int? Level { get; set; }

        [Column("Level_Water")]
        [DisplayName("WATER LEVEL")]
        public int? Level_Water { get; set; }

        [Column("Temperature")]
        [DisplayName("TEMPERATURE")]
        public double? Temperature { get; set; }

        [Column("Density")]
        [DisplayName("DENSITY")]
        public double? Density { get; set; }

        [Column("Volume")]
        [DisplayName("VOLUME")]
        public double? Volume { get; set; }

        [Column("Flowrate")]
        [DisplayName("FLOWRATE")]
        public double? Flowrate { get; set; }

        [Column("TargetLevel")]
        [DisplayName("TARGET LEVEL")]
        public int? TargetLevel { get; set; }

        [Column("LastFlowrateChangeTime")]
        [DisplayName("LAST FLOWRATE CHANGE")]
        [DataType(DataType.DateTime)]
        public DateTime? LastFlowrateChangeTime { get; set; }

        [Column("StagnantAlarm")]
        [DisplayName("STAGNANT ALARM")]
        public bool StagnantAlarm { get; set; }

        [Column("StagnantThresholdMinutes")]
        [DisplayName("STAGNANT THRESHOLD (MINUTES)")]
        public int? StagnantThresholdMinutes { get; set; }

        [Column("Status")]
        [Display(Name = "STATUS")]
        public int Status { get; set; }

        [Column("IsManuallyConfigured")]
        [DisplayName("MANUALLY CONFIGURED")]
        public bool IsManuallyConfigured { get; set; }

        [Column("OperationAlarmAck")]
        [DisplayName("OPERATION ALARM ACK")]
        public bool OperationAlarmAck { get; set; } = false;

        // ✅ NEW: Disable Operation Alarm flag (khusus untuk T-10 dan T-11)
        [Column("DisableOperationAlarm")]
        [DisplayName("DISABLE OPERATION ALARM")]
        public bool DisableOperationAlarm { get; set; } = false;

        [Column("IsLevel")]
        [DisplayName("IS LEVEL")]
        public bool? IsLevel { get; set; }

        [Column("AlarmTimeStamp")]
        [DisplayName("ALARM TIME")]
        [DataType(DataType.Time)]
        public TimeSpan? AlarmTimeStamp { get; set; }

        [Column("EstimationTimeStamp")]
        [DisplayName("ESTIMATED TIME")]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public TimeSpan? EstimationTimeStamp { get; set; }

        [NotMapped]
        public string EstimatedTimeString
        {
            get
            {
                if (EstimationTimeStamp == null) return null;
                return ((TimeSpan)EstimationTimeStamp).ToString(@"hh\:mm\:ss");
            }
        }

        [NotMapped]
        private bool _alarm;

        [NotMapped]
        public bool Alarm
        {
            get { return _alarm; }
            set { if (_alarm != value) _alarm = value; }
        }

        [NotMapped]
        public string AlarmTimeString
        {
            get
            {
                if (AlarmTimeStamp == null) return null;
                return ((TimeSpan)AlarmTimeStamp).ToString(@"hh\:mm\:ss");
            }
        }

        public virtual Tank? tank { get; set; }
    }
}
