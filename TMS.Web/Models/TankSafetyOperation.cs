using System.ComponentModel.DataAnnotations;

namespace TMS.Web.Models
{
    public class TankSafetyOperation
    {
        public string TankName { get; set; }
        
        [Display(Name = "Max Level")]
        public double MaxLevel { get; set; }
        
        [Display(Name = "Min Level")]
        public double MinLevel { get; set; }
        
        [Display(Name = "Operation Type")]
        public string OperationType { get; set; }
        
        [Display(Name = "Target Level")]
        public int? TargetLevel { get; set; }
        
        [Display(Name = "Stagnant Threshold (minutes)")]
        public int? StagnantThresholdMinutes { get; set; }
        
        /// <summary>
        /// ✅ Disable Operation Alarm - KHUSUS T-10 dan T-11
        /// Jika true, alarm operasi (Stagnant, Target, dll) tidak akan berbunyi
        /// </summary>
        [Display(Name = "Disable Operation Alarm")]
        public bool DisableOperationAlarm { get; set; }
        
        public string Desc { get; set; }
    }
}
