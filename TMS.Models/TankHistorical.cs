// AUTO-GENERATED from database schema
// Database: TAS_CIKAMPEK2014
// Table: Tank_HistoricalData
// Generated: 12/05/2025 14:55:31

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMS.Models
{
    [Table("Tank_HistoricalData")]
    public class TankHistorical
    {
        [Key]
        [Column("Historical_ID")]
        [DisplayName("ID")]
        public int Id { get; set; }

        [Column("Tank_Number")]
        [StringLength(50)]
        [ForeignKey("Tank")]
        [DisplayName("TANK")]
        public string Tank_Number { get; set; }

        [Column("TimeStamp")]
        [DisplayName("TIMESTAMP")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy HH:mm:ss}")]
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
        [DisplayName("TEMPERATURE")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Temperature { get; set; }

        [Column("Density")]
        [DisplayName("DENSITY")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Density { get; set; }

        [Column("Volume_Obs")]
        [DisplayName("VOLUME OBS")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Volume_Obs { get; set; }

        [Column("Density_Std")]
        [DisplayName("DENSITY STD")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Density_Std { get; set; }

        [Column("Volume_Std")]
        [DisplayName("VOLUME STD")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Volume_Std { get; set; }

        [Column("Volume_LongTons")]
        [DisplayName("VOLUME LONG TONS")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Volume_LongTons { get; set; }

        [Column("Volume_BBL60F")]
        [DisplayName("VOLUME BBL60F")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Volume_BBL60F { get; set; }

        [Column("Flowrate")]
        [DisplayName("FLOWRATE")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Flowrate { get; set; }

        // ✅ NEW COLUMNS - Added 2025-12-16
        [Column("Pumpable")]
        [DisplayName("PUMPABLE")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Pumpable { get; set; }

        [Column("Ullage")]
        [DisplayName("ULLAGE")]
        [DisplayFormat(DataFormatString = "{0:0.0000}", ApplyFormatInEditMode = true)]
        public double? Ullage { get; set; }

        [Column("Alarm_Status")]
        [DisplayName("ALARM STATUS")]
        public int? Alarm_Status { get; set; }

        [Column("AlarmMessage")]
        [StringLength(100)]
        [DisplayName("ALARM MESSAGE")]
        public string? AlarmMessage { get; set; }

        // Navigation property
        public Tank? Tank { get; set; }
    }
}
