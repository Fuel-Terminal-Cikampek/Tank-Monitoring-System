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
    public class TankLiveData
    {
        //Foreign Key - mapped to Tank_Number in database
        [Column("Tank_Number")]
        public string Tank_Number { get; set; }

        [Column("Product_ID")]
        [StringLength(20)]
        public string Product_ID { get; set; }  // Store Product_Code (string), not GUID

        [Column("TimeStamp")]
        [DisplayName("TIMESTAMP")]
        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy}")]
        public DateTime? TimeStamp { get; set; }

        [Column("Level")]
        [DisplayName("LEVEL")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public int? Level { get; set; }

        [Column("Level_Water")]
        [DisplayName("WATER LEVEL")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public int? Level_Water { get; set; }

        [Column("Temperature")]
        [DisplayName("MAT TEMP")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Temperature { get; set; }

        [Column("Density")]
        [DisplayName("DENSITY")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Density { get; set; }

        [Column("Volume_Obs")]
        [DisplayName("VOLUME(L)")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Volume_Obs { get; set; }

        [Column("Density_Std")]
        public double? Density_Std { get; set; }

        [Column("Volume_Std")]
        public double? Volume_Std { get; set; }

        [Column("Volume_LongTons")]
        public double? Volume_LongTons { get; set; }

        [Column("Volume_BBL60F")]
        public double? Volume_BBL60F { get; set; }

        [Column("Flowrate")]
        [DisplayName("FLOW RATE")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Flowrate { get; set; }

        [Column("Alarm_Status")]
        public int? Alarm_Status { get; set; }

        [Column("Level_DeadStock")]
        public int? Level_DeadStock { get; set; }

        [Column("Volume_DeadStock")]
        public double? Volume_DeadStock { get; set; }

        [Column("Level_Safe_Capacity")]
        public int? Level_Safe_Capacity { get; set; }

        [Column("Volume_Safe_Capacity")]
        public double? Volume_Safe_Capacity { get; set; }

        // ============ LEGACY COLUMNS - NOT IN DATABASE (USE Tank_LiveDataTMS) ============
        // ⚠️ These columns are NOT in Tank_LiveData database (moved to Tank_LiveDataTMS)
        // ⚠️ Tank_LiveData (this table) = 18 columns (CLEAN - no legacy columns)
        // ⚠️ Tank_LiveDataTMS (new table) = 27 columns (18 clean + 9 legacy)
        // ⚠️ Both tables updated in parallel by ATG Service
        // ⚠️ For queries using these legacy columns, use Tank_LiveDataTMS instead

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        public bool? Ack { get; set; }

        [NotMapped]  // This is for UI only, not in database
        public DateTime? AckTimestamp { get; set; }

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        public double? FlowRateMperSecond { get; set; }

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        public double? LastLiquidLevel { get; set; }

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        public DateTime? LastTimeStamp { get; set; }

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        public int? TotalSecond { get; set; }

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        public double? LastVolume { get; set; }

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        [DisplayName("PUMPABLE(L)")]
        public double? Pumpable { get; set; }

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        [StringLength(100)]
        public string? AlarmMessage { get; set; }

        // ============ LEGACY PROPERTY MAPPINGS FOR BACKWARD COMPATIBILITY ============
        // These properties provide backward compatibility with TMSJAMBI code

        // Backward compatibility - TankId auto-calculated from Tank_Number
        [NotMapped]
        public int TankId
        {
            get => Tank_Number?.GetHashCode() ?? 0;
            set { /* ignored - shadow property */ }
        }

        [NotMapped]
        public double LiquidLevel { get => Level ?? 0; set => Level = (int)value; }

        [NotMapped]
        public double WaterLevel { get => Level_Water ?? 0; set => Level_Water = (int)value; }

        [NotMapped]
        public double LiquidTemperature { get => Temperature ?? 0; set => Temperature = value; }

        [NotMapped]
        public double LiquidDensity { get => Density ?? 0; set => Density = value; }

        [NotMapped]
        [DisplayName("VOLUME(L)")]
        public double Volume { get => Volume_Obs ?? 0; set => Volume_Obs = value; }

        [NotMapped]  // Column moved to Tank_LiveDataTMS
        [DisplayName("ULLAGE(L)")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double Ullage { get; set; }

        [NotMapped]
        public double FlowRate { get => Flowrate ?? 0; set => Flowrate = value; }

        [NotMapped]
        public double FlowRateKLperH { get => Flowrate ?? 0; set => Flowrate = value; }

        [NotMapped]
        [DisplayName("STATUS")]
        public string? Status { get; set; }

        [NotMapped]
        public string? AlarmStatus { get; set; }

        [NotMapped]
        public virtual Tank tank { get; set; }
        [NotMapped]
        public bool FlowrateJustCalculated { get; set; }
        [NotMapped]
        public int FlowrateHoldRemaining { get; set; } = 0;
        [NotMapped]
        public double? LastValidFlowrate { get; set; }

    }
}
