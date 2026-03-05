using System;
using System.Linq;
using TMS.ATGService.Service;

namespace TMS.ATGService
{
    /// <summary>
    /// Quick test untuk verify FDInterfaceXmlReader
    ///
    /// Usage:
    /// 1. Temporarily rename RunTest() to Main()
    /// 2. Comment out Program.cs Main method
    /// 3. Run: dotnet run
    /// 4. Restore changes after testing
    ///
    /// Or run directly: TestXmlReader.RunTest();
    /// </summary>
    public class TestXmlReader
    {
        public static void RunTest(string[] args = null)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("TMS.ATGService - FD-INTERFACE XML Reader Test");
            Console.WriteLine("==============================================\n");

            // Path ke XML file dari Refrensi folder
            string xmlPath = @"C:\Kerjaan\TMS Cikampek\Refrensi\FD-INTERFACE N NEW\system.xml";

            try
            {
                // Create XML reader
                var reader = new FDInterfaceXmlReader(xmlPath, null);
                Console.WriteLine($"✅ XML Reader initialized");
                Console.WriteLine($"📁 File: {xmlPath}\n");

                // Read XML
                var xmlData = reader.ReadXml();
                if (xmlData == null || xmlData.Tanks == null)
                {
                    Console.WriteLine("❌ Failed to read XML data");
                    return;
                }

                Console.WriteLine($"✅ XML parsed successfully");
                Console.WriteLine($"📊 Found {xmlData.Tanks.Count} tanks\n");

                // Display summary
                Console.WriteLine("=== Tank Data Summary ===\n");
                foreach (var tank in xmlData.Tanks.OrderBy(t => t.TankId))
                {
                    Console.WriteLine($"Tank: {tank.TankId}");
                    Console.WriteLine($"  Product    : {tank.Product.Value}");
                    Console.WriteLine($"  Level      : {tank.Level.Value} mm");
                    Console.WriteLine($"  Temperature: {tank.Temperature.Value} °C");
                    Console.WriteLine($"  Density    : {tank.Density.Value} kg/m³");
                    Console.WriteLine($"  Volume Obs : {tank.GrossObsVolume.Value} L");
                    Console.WriteLine($"  Volume Std : {tank.NetStdVolume.Value} L");
                    Console.WriteLine();
                }

                // Convert to TankLiveData
                Console.WriteLine("\n=== Converting to TankLiveData Model ===\n");
                var tankLiveDataList = reader.ConvertToTankLiveData();
                Console.WriteLine($"✅ Converted {tankLiveDataList.Count} tanks to TankLiveData");

                // Display first 3 converted items
                Console.WriteLine("\n=== Sample Converted Data (First 3) ===\n");
                foreach (var item in tankLiveDataList.Take(3))
                {
                    Console.WriteLine($"Tank_Number : {item.Tank_Number}");
                    Console.WriteLine($"Product_ID  : {item.Product_ID}");
                    Console.WriteLine($"Level       : {item.Level} mm");
                    Console.WriteLine($"Temperature : {item.Temperature} °C");
                    Console.WriteLine($"Density     : {item.Density} kg/m³");
                    Console.WriteLine($"Volume_Obs  : {item.Volume_Obs} L");
                    Console.WriteLine($"TimeStamp   : {item.TimeStamp}");
                    Console.WriteLine();
                }

                Console.WriteLine("\n==============================================");
                Console.WriteLine("✅ TEST PASSED - XML Reader Working!");
                Console.WriteLine("==============================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ ERROR: {ex.Message}");
                Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
            }
        }
    }
}
