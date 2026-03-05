using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TMS.ATGService.Models;
using TMS.Models;

namespace TMS.ATGService.Service
{
    /// <summary>
    /// Service untuk read dan parse system.xml dari FD-INTERFACE
    ///
    /// Usage:
    /// var reader = new FDInterfaceXmlReader("path/to/system.xml");
    /// var tankData = reader.ReadTankData();
    /// </summary>
    public class FDInterfaceXmlReader
    {
        private readonly string _xmlFilePath;
        private readonly ILogger<FDInterfaceXmlReader>? _logger;
        private DateTime _lastReadTime = DateTime.MinValue;
        private FDInterfaceSystem? _cachedData = null;

        public FDInterfaceXmlReader(string xmlFilePath, ILogger<FDInterfaceXmlReader>? logger = null)
        {
            _xmlFilePath = xmlFilePath;
            _logger = logger;

            if (!File.Exists(_xmlFilePath))
            {
                throw new FileNotFoundException($"FD-INTERFACE XML file not found: {_xmlFilePath}");
            }
        }

        /// <summary>
        /// Read dan parse XML file dari FD-INTERFACE
        /// Includes caching - hanya re-read jika file berubah
        /// </summary>
        public FDInterfaceSystem? ReadXml()
        {
            try
            {
                var fileInfo = new FileInfo(_xmlFilePath);

                // Check if file has been modified since last read
                if (_cachedData != null && fileInfo.LastWriteTime <= _lastReadTime)
                {
                    _logger?.LogDebug("Using cached XML data (file not modified)");
                    return _cachedData;
                }

                _logger?.LogInformation($"Reading FD-INTERFACE XML from: {_xmlFilePath}");

                var serializer = new XmlSerializer(typeof(FDInterfaceSystem));

                using (var reader = new StreamReader(_xmlFilePath))
                {
                    _cachedData = (FDInterfaceSystem?)serializer.Deserialize(reader);
                    _lastReadTime = DateTime.Now;

                    _logger?.LogInformation($"Successfully parsed XML - Found {_cachedData?.Tanks.Count ?? 0} tanks");
                    return _cachedData;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error reading FD-INTERFACE XML: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert FD-INTERFACE XML data ke TankLiveData model
        /// </summary>
        public List<TankLiveData> ConvertToTankLiveData()
        {
            var xmlData = ReadXml();
            if (xmlData == null || xmlData.Tanks == null)
            {
                return new List<TankLiveData>();
            }

            var tankLiveDataList = new List<TankLiveData>();

            foreach (var xmlTank in xmlData.Tanks)
            {
                try
                {
                    // ===== CONSOLE DEBUG: Show XML raw values =====
                    Console.WriteLine($"[{xmlTank.TankId}] Reading XML data...");
                    Console.WriteLine($"  - Product: {xmlTank.Product.Value}");

                    // ===== CRITICAL: Level validation before conversion =====
                    string levelRawValue = xmlTank.Level.Value;
                    Console.WriteLine($"  - Level (RAW): '{levelRawValue}' (length={levelRawValue.Length})");

                    // Validate Level value before ToInt()
                    if (string.IsNullOrWhiteSpace(levelRawValue))
                    {
                        Console.WriteLine($"  ⚠️ WARNING: Level value is EMPTY/NULL for tank {xmlTank.TankId}!");
                    }

                    int levelConverted = xmlTank.Level.ToInt();
                    Console.WriteLine($"  - Level (CONVERTED): {levelConverted} mm");

                    if (levelConverted == 0 && !string.IsNullOrWhiteSpace(levelRawValue) && levelRawValue != "0")
                    {
                        Console.WriteLine($"  ⚠️ WARNING: Level conversion resulted in 0 but raw value was '{levelRawValue}' - PARSE FAILURE SUSPECTED!");
                    }

                    Console.WriteLine($"  - Temperature: {xmlTank.Temperature.Value}°C");

                    // Density - KONVERSI dari kg/m³ ke g/cm³
                    double densityRaw = xmlTank.Density.ToDouble();
                    double densityConverted = densityRaw / 1000.0;  // kg/m³ → g/cm³
                    Console.WriteLine($"  - Density: {densityRaw:F3} kg/m³ → {densityConverted:F3} g/cm³");

                    // Volume (TIDAK ADA KONVERSI - sudah dalam m³ dari FD-Interface)
                    double volumeRaw = xmlTank.GrossObsVolume.ToDouble();
                    double volumeConverted = volumeRaw;  // LANGSUNG PAKAI - sudah m³
                    Console.WriteLine($"  - Volume: {volumeRaw:F2} m³ (SUDAH SESUAI FD-INTERFACE)");

                    var tankLiveData = new TankLiveData
                    {
                        // Tank identifier
                        Tank_Number = xmlTank.TankId, // e.g., "T-1", "T-2", etc.

                        // Product info (get from XML, but might need mapping)
                        Product_ID = xmlTank.Product.Value, // e.g., "PREMIUM", "SOLAR"

                        // Timestamp
                        TimeStamp = DateTime.Now, // FD-INTERFACE doesn't provide timestamp in XML

                        // Level data (in mm)
                        Level = levelConverted,
                        Level_Water = xmlTank.WaterLevel.ToInt(),

                        // Temperature (in Celsius) - use ToDouble() for SQL Server float type
                        Temperature = xmlTank.Temperature.ToDouble(),

                        // Density - KONVERSI dari kg/m³ (FD-Interface) ke g/cm³ (database)
                        // FD-Interface: 789 kg/m³ → 0.789 g/cm³
                        Density = densityConverted,  // Sudah di-konversi ÷1000

                        // Volume - LANGSUNG DARI FD-INTERFACE (sudah dalam m³)
                        // FD-INTERFACE XML sudah memberikan volume dalam m³
                        // TIDAK PERLU KONVERSI - langsung pakai!
                        Volume_Obs = volumeConverted,  // Gross Observed Volume (m³)
                        Volume_Std = xmlTank.GrossStdVolume.ToDouble(),  // Gross Standard Volume (m³)

                        // Note: Some fields tidak ada di XML FD-INTERFACE
                        // Akan diisi dengan default atau dihitung dari data lain
                        Density_Std = 0, // Not in XML
                        Volume_LongTons = 0, // Not in XML
                        Volume_BBL60F = 0, // Not in XML
                        Flowrate = 0, // Not in XML - will be calculated by service
                        Alarm_Status = null, // INT - NULL untuk normal (akan di-set oleh WorkerXmlMode)
                        Level_DeadStock = 0, // Not in XML
                        Volume_DeadStock = 0, // Not in XML
                        Level_Safe_Capacity = 0, // Not in XML
                        Volume_Safe_Capacity = 0, // Not in XML

                        // New fields
                        // ✅ FIX: Set Ack = true (ready state) untuk data baru, akan di-restore dari DB jika sudah ada
                        Ack = true,
                        FlowRateMperSecond = 0,
                        LastLiquidLevel = 0,
                        LastTimeStamp = null,
                        TotalSecond = 0,
                        LastVolume = 0,
                        Pumpable = 0,
                        AlarmMessage = null
                    };

                    tankLiveDataList.Add(tankLiveData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ ERROR converting tank {xmlTank.TankId}: {ex.Message}");
                    Console.WriteLine($"  Stack Trace: {ex.StackTrace}");
                    Console.WriteLine(""); // Empty line for readability
                    _logger?.LogError(ex, $"Error converting tank {xmlTank.TankId}: {ex.Message}");
                }
            }


            return tankLiveDataList;
        }

        /// <summary>
        /// Get summary information tentang data yang dibaca
        /// </summary>
        public string GetSummary()
        {
            var xmlData = ReadXml();
            if (xmlData == null)
            {
                return "No data available";
            }

            var tanks = xmlData.Tanks;
            var summary = $"FD-INTERFACE Data Summary:\n";
            summary += $"Total Tanks: {tanks.Count}\n";
            summary += $"Last Read: {_lastReadTime:yyyy-MM-dd HH:mm:ss}\n";
            summary += $"\nTanks:\n";

            foreach (var tank in tanks.OrderBy(t => t.TankId))
            {
                summary += $"  {tank.TankId}: {tank.Product.Value} - Level: {tank.Level.Value}mm, Temp: {tank.Temperature.Value}°C\n";
            }

            return summary;
        }
    }
}
