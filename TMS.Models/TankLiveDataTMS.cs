using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Models
{
    /// <summary>
    /// ✅ NEW MODEL: Tank Live Data TMS (FULL VERSION with Legacy Columns)
    ///
    /// Purpose: Data TMS yang LENGKAP dengan kolom legacy untuk backward compatibility
    /// - Tank_LiveData: CLEAN version (18 kolom) - legacy columns removed
    /// - Tank_LiveDataTMS: FULL version (27 kolom = 18 clean + 9 legacy)
    ///
    /// LEGACY COLUMNS (included in this model for backward compatibility):
    /// - Ack, FlowRateMperSecond, LastLiquidLevel, LastTimeStamp
    /// - TotalSecond, LastVolume, Pumpable, AlarmMessage, Ullage
    ///
    /// Both tables updated in parallel by ATG Service
    /// </summary>
    public class TankLiveDataTMS
    {
        // ============ PRIMARY KEY ============
        [Key]
        [Column("Tank_Number")]
        public string Tank_Number { get; set; }

        // ============ PRODUCT & TIME ============
        [Column("Product_ID")]
        [StringLength(20)]
        public string Product_ID { get; set; }

        [Column("TimeStamp")]
        [DisplayName("TIMESTAMP")]
        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy}")]
        public DateTime? TimeStamp { get; set; }

        // ============ MEASUREMENTS ============
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

        // ============ VOLUMES ============
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

        // ============ FLOW & ALARMS ============
        [Column("Flowrate")]
        [DisplayName("FLOW RATE")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Flowrate { get; set; }

        [Column("Alarm_Status")]
        public int? Alarm_Status { get; set; }

        // ============ CAPACITY LEVELS ============
        [Column("Level_DeadStock")]
        public int? Level_DeadStock { get; set; }

        [Column("Volume_DeadStock")]
        public double? Volume_DeadStock { get; set; }

        [Column("Level_Safe_Capacity")]
        public int? Level_Safe_Capacity { get; set; }

        [Column("Volume_Safe_Capacity")]
        public double? Volume_Safe_Capacity { get; set; }

        // ============ LEGACY COLUMNS FOR BACKWARD COMPATIBILITY ============
        // These 9 columns provide backward compatibility with existing TMS.Web code
        // that still uses Ack, AlarmMessage, Pumpable, Ullage, TotalSecond, etc.

        [Column("Ack")]
        public bool? Ack { get; set; }

        [Column("FlowRateMperSecond")]
        public double? FlowRateMperSecond { get; set; }

        [Column("LastLiquidLevel")]
        public double? LastLiquidLevel { get; set; }

        [Column("LastTimeStamp")]
        public DateTime? LastTimeStamp { get; set; }

        [Column("TotalSecond")]
        public int? TotalSecond { get; set; }

        [Column("LastVolume")]
        public double? LastVolume { get; set; }

        [Column("Pumpable")]
        [DisplayName("PUMPABLE(L)")]
        public double? Pumpable { get; set; }

        [Column("AlarmMessage")]
        [StringLength(100)]
        public string? AlarmMessage { get; set; }

        [Column("Ullage")]
        [DisplayName("ULLAGE(L)")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Ullage { get; set; }

        // ============ BACKWARD COMPATIBILITY PROPERTIES ============
        // These properties provide backward compatibility with TMSJAMBI code
        // Mapped to actual database columns using getters/setters

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

        [NotMapped]
        public double FlowRate { get => Flowrate ?? 0; set => Flowrate = value; }

        [NotMapped]
        public double FlowRateKLperH { get => Flowrate ?? 0; set => Flowrate = value; }

        // ============ HELPER METHODS ============

        /// <summary>
        /// Convert dari TankLiveData ke TankLiveDataTMS
        /// Copy SEMUA kolom (18 clean + 9 legacy = 27 kolom total)
        /// </summary>
        public static TankLiveDataTMS FromTankLiveData(TankLiveData source)
        {
            if (source == null) return null;

            return new TankLiveDataTMS
            {
                // 18 Clean columns
                Tank_Number = source.Tank_Number,
                Product_ID = source.Product_ID,
                TimeStamp = source.TimeStamp,
                Level = source.Level,
                Level_Water = source.Level_Water,
                Temperature = source.Temperature,
                Density = source.Density,
                Volume_Obs = source.Volume_Obs,
                Density_Std = source.Density_Std,
                Volume_Std = source.Volume_Std,
                Volume_LongTons = source.Volume_LongTons,
                Volume_BBL60F = source.Volume_BBL60F,
                Flowrate = source.Flowrate,
                Alarm_Status = source.Alarm_Status,
                Level_DeadStock = source.Level_DeadStock,
                Volume_DeadStock = source.Volume_DeadStock,
                Level_Safe_Capacity = source.Level_Safe_Capacity,
                Volume_Safe_Capacity = source.Volume_Safe_Capacity,

                // 9 Legacy columns
                Ack = source.Ack,
                FlowRateMperSecond = source.FlowRateMperSecond,
                LastLiquidLevel = source.LastLiquidLevel,
                LastTimeStamp = source.LastTimeStamp,
                TotalSecond = source.TotalSecond,
                LastVolume = source.LastVolume,
                Pumpable = source.Pumpable,
                AlarmMessage = source.AlarmMessage,
                Ullage = source.Ullage
            };
        }

        /// <summary>
        /// Clone/copy constructor untuk update (all 27 columns)
        /// </summary>
        public void CopyFrom(TankLiveDataTMS source)
        {
            if (source == null) return;

            // 18 Clean columns
            this.Product_ID = source.Product_ID;
            this.TimeStamp = source.TimeStamp;
            this.Level = source.Level;
            this.Level_Water = source.Level_Water;
            this.Temperature = source.Temperature;
            this.Density = source.Density;
            this.Volume_Obs = source.Volume_Obs;
            this.Density_Std = source.Density_Std;
            this.Volume_Std = source.Volume_Std;
            this.Volume_LongTons = source.Volume_LongTons;
            this.Volume_BBL60F = source.Volume_BBL60F;
            this.Flowrate = source.Flowrate;
            this.Alarm_Status = source.Alarm_Status;
            this.Level_DeadStock = source.Level_DeadStock;
            this.Volume_DeadStock = source.Volume_DeadStock;
            this.Level_Safe_Capacity = source.Level_Safe_Capacity;
            this.Volume_Safe_Capacity = source.Volume_Safe_Capacity;

            // 9 Legacy columns
            this.Ack = source.Ack;
            this.FlowRateMperSecond = source.FlowRateMperSecond;
            this.LastLiquidLevel = source.LastLiquidLevel;
            this.LastTimeStamp = source.LastTimeStamp;
            this.TotalSecond = source.TotalSecond;
            this.LastVolume = source.LastVolume;
            this.Pumpable = source.Pumpable;
            this.AlarmMessage = source.AlarmMessage;
            this.Ullage = source.Ullage;
        }
    }
}
