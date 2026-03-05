namespace TMS.Web.Models
{
    /// <summary>
    /// Configuration for UI refresh intervals (in seconds)
    /// </summary>
    public class UIRefreshSettings
    {
        /// <summary>
        /// Refresh interval for Tank Live Data page (in seconds)
        /// </summary>
        public int TankLiveDataRefreshSeconds { get; set; } = 10;

        /// <summary>
        /// Refresh interval for Tank Movement page (in seconds)
        /// </summary>
        public int TankMovementRefreshSeconds { get; set; } = 10;

        /// <summary>
        /// Refresh interval for Tank Ticket page (in seconds)
        /// </summary>
        public int TankTicketRefreshSeconds { get; set; } = 10;
    }
}
