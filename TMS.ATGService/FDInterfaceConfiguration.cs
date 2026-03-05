using System;

namespace TMS.ATGService
{
    /// <summary>
    /// Configuration untuk FD-INTERFACE XML Reader mode
    /// </summary>
    public class FDInterfaceConfiguration
    {
        /// <summary>
        /// Path ke system.xml file dari FD-INTERFACE
        /// Example: "C:\\FD-INTERFACE\\system.xml"
        /// </summary>
        public string XmlFilePath { get; set; } = string.Empty;

        /// <summary>
        /// HTTP endpoint untuk get data dari FD-INTERFACE
        /// Example: "http://192.168.2.53:8002/getalltankdata"
        /// </summary>
        public string HttpEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Use HTTP mode instead of file reading
        /// true = Read via HTTP endpoint
        /// false = Read from XML file
        /// </summary>
        public bool UseHttpMode { get; set; } = false;

        /// <summary>
        /// Interval polling XML file (in seconds)
        /// Default: 5 seconds
        /// </summary>
        public int PollingIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Enable XML mode (true) or Modbus mode (false)
        /// true = Read from FD-INTERFACE XML
        /// false = Direct Modbus communication to ATG devices
        /// </summary>
        public bool EnableXmlMode { get; set; } = true;

        /// <summary>
        /// Description/notes about this configuration
        /// </summary>
        public string Description { get; set; } = string.Empty;

        // =========================================================
        // ZERO LEVEL PROTECTION CONFIGURATION
        // =========================================================

        /// <summary>
        /// Maximum retry attempts when Level=0 is detected
        /// Default: 3 retries
        /// </summary>
        public int ZeroRetryMaxAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retries in milliseconds (exponential backoff)
        /// Example: [100, 300, 500] means 100ms, 300ms, 500ms delays
        /// Default: 100ms, 300ms, 500ms
        /// </summary>
        public int[] ZeroRetryDelaysMs { get; set; } = new int[] { 100, 300, 500 };

        /// <summary>
        /// Number of consecutive zero readings before confirming zero
        /// Default: 25 cycles
        /// </summary>
        public int ZeroHoldCycles { get; set; } = 25;

        /// <summary>
        /// Maximum allowed level change per cycle (mm)
        /// If exceeded, treat as invalid reading (likely sensor error)
        /// Default: 100mm per cycle (realistic max drain/fill rate)
        /// </summary>
        public double MaxDeltaLevelPerCycle { get; set; } = 100.0;

        /// <summary>
        /// Enable cross-reference validation
        /// If Level=0 but Temperature/Density are normal, treat as comm error
        /// Default: true
        /// </summary>
        public bool EnableCrossReferenceValidation { get; set; } = true;

        /// <summary>
        /// Minimum temperature threshold for cross-reference validation
        /// If temperature below this, tank might actually be empty
        /// Default: 15.0°C
        /// </summary>
        public double CrossRefMinTemperature { get; set; } = 15.0;

        /// <summary>
        /// Maximum temperature threshold for cross-reference validation
        /// If temperature above this, sensor might be malfunctioning
        /// Default: 50.0°C
        /// </summary>
        public double CrossRefMaxTemperature { get; set; } = 50.0;

        /// <summary>
        /// Minimum density threshold for cross-reference validation
        /// Default: 0.7 kg/L (typical fuel minimum)
        /// </summary>
        public double CrossRefMinDensity { get; set; } = 0.7;

        /// <summary>
        /// Maximum density threshold for cross-reference validation
        /// Default: 1.0 kg/L (typical fuel maximum)
        /// </summary>
        public double CrossRefMaxDensity { get; set; } = 1.0;
    }
}
