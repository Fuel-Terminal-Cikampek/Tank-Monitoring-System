using System;

namespace TMS.Web.Extensions
{
    public static class TypeConversionExtensions
    {
        public static double ToDouble(this int? value)
        {
            return value.HasValue ? (double)value.Value : 0.0;
        }

        public static double ToDouble(this int value)
        {
            return (double)value;
        }
    }
}