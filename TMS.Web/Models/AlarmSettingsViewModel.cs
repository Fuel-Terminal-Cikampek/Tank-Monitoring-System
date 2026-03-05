namespace TMS.Web.Models
{
    public class AlarmSettingsViewModel
    {
        // Stagnant Detection
        public double StagnantFlowrateToleranceKLH { get; set; } = 0.05;
        public int DefaultStagnantThresholdMinutes { get; set; } = 5;
        
        // Standby Anomaly
        public double StandbyFlowrateToleranceKLH { get; set; } = 20.0;
        public int StandbyLevelChangeToleranceMM { get; set; } = 10;
        
        // Sales Anomaly
        public double SalesFlowrateToleranceKLH { get; set; } = 20.0;
        public int SalesLevelChangeToleranceMM { get; set; } = 5;
        
        // Level Fluctuation
        public int LevelFluctuationToleranceMM { get; set; } = 2;
        
        // Flag untuk UI
        public bool CanEdit { get; set; } = false;
    }
}