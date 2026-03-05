using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSL.Web
{
    public class SAPTime
    {
        // sample DDMMYYYY
        public static DateTime FromSAPDate(string date)
        {
            if (date == "")
                return DateTime.MinValue;

            string day = date.Substring(0, 2);
            string month = date.Substring(2, 2);
            string year = date.Substring(4, 4);

            if (day == "00" && month == "00" && year == "0000")
                return DateTime.MinValue;
            try
            {
                return new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), Convert.ToInt32(day));
            }
            catch (Exception ex)
            {
                return DateTime.MinValue;
            }
        }
        public static DateTime FromSAPDateTime(string time)
        {
            string hour = time.Substring(0, 2);
            string minute = time.Substring(2, 2);
            string second = time.Substring(4, 2);
            return DateTime.Now;
        }
        public static DateTime FromSAPDateTime(string date, string time)
        {
            if (date == "" || time == "")
                return DateTime.MinValue;

            string day = date.Substring(0, 2);
            string month = date.Substring(2, 2);
            string year = date.Substring(4, 4);

            string hour = time.Substring(0, 2);
            string minute = time.Substring(2, 2);
            string second = time.Substring(4, 2);

            if (day == "00" && month == "00" && year == "00")
                return DateTime.MinValue;

            try
            {
                return new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), Convert.ToInt32(day))
                   .AddHours(Convert.ToInt32(hour))
                   .AddMinutes(Convert.ToInt32(minute))
                   .AddSeconds(Convert.ToInt32(second));
            }
            catch (Exception ex)
            {
                return DateTime.MinValue;
            }
        }
    }
}
