using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TMS.ATGService.Models
{
    /// <summary>
    /// Model untuk parse system.xml dari FD-INTERFACE
    /// XML Structure:
    /// <system>
    ///   <tank tankid="T-1">
    ///     <product value="PREMIUM"/>
    ///     <level value="3700"/>
    ///     <temperature value="29.65"/>
    ///     ...
    ///   </tank>
    /// </system>
    /// </summary>
    [XmlRoot("system")]
    public class FDInterfaceSystem
    {
        [XmlElement("tank")]
        public List<FDInterfaceTank> Tanks { get; set; } = new List<FDInterfaceTank>();
    }

    public class FDInterfaceTank
    {
        [XmlAttribute("tankid")]
        public string TankId { get; set; } = string.Empty;

        [XmlElement("product")]
        public XmlValue Product { get; set; } = new XmlValue();

        [XmlElement("level")]
        public XmlValue Level { get; set; } = new XmlValue();

        [XmlElement("temperature")]
        public XmlValue Temperature { get; set; } = new XmlValue();

        [XmlElement("density")]
        public XmlValue Density { get; set; } = new XmlValue();

        [XmlElement("waterlevel")]
        public XmlValue WaterLevel { get; set; } = new XmlValue();

        [XmlElement("pressure")]
        public XmlValue Pressure { get; set; } = new XmlValue();

        [XmlElement("netstdvolume")]
        public XmlValue NetStdVolume { get; set; } = new XmlValue();

        [XmlElement("grossobsvolume")]
        public XmlValue GrossObsVolume { get; set; } = new XmlValue();

        [XmlElement("grossstdvolume")]
        public XmlValue GrossStdVolume { get; set; } = new XmlValue();

        [XmlElement("address")]
        public XmlValue Address { get; set; } = new XmlValue();
    }

    public class XmlValue
    {
        [XmlAttribute("value")]
        public string Value { get; set; } = string.Empty;

        public int ToInt()
        {
            if (int.TryParse(Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            else
            {
                // ⚠️ LOGGING: Parse failure detected
                string displayValue = string.IsNullOrEmpty(Value) ? "[EMPTY/NULL]" : $"'{Value}'";
                Console.WriteLine($"⚠️ [PARSE WARNING] ToInt() failed for value {displayValue}, returning 0");
                return 0;
            }
        }

        public double ToDouble()
        {
            if (double.TryParse(Value, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            else
            {
                // ⚠️ LOGGING: Parse failure detected
                string displayValue = string.IsNullOrEmpty(Value) ? "[EMPTY/NULL]" : $"'{Value}'";
                Console.WriteLine($"⚠️ [PARSE WARNING] ToDouble() failed for value {displayValue}, returning 0.0");
                return 0.0;
            }
        }

        public float ToFloat()
        {
            if (float.TryParse(Value, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            else
            {
                // ⚠️ LOGGING: Parse failure detected
                string displayValue = string.IsNullOrEmpty(Value) ? "[EMPTY/NULL]" : $"'{Value}'";
                Console.WriteLine($"⚠️ [PARSE WARNING] ToFloat() failed for value {displayValue}, returning 0.0f");
                return 0.0f;
            }
        }
    }
}
