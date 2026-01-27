using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ManagerClient.Converters

{
    internal class NearDeadlineConverter : IMultiValueConverter
    {
        public int DaysThreshold { get; set; } = 2;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            if (values[0] is not DateTime rok || values[1] is null)
                return false;

            
            string statusText = values[1].ToString() ?? "";
            if (statusText.Equals("Zavrsen", StringComparison.OrdinalIgnoreCase))
                return false;

            var now = DateTime.Now;
            var diff = rok - now;

            return diff.TotalDays >= 0 && diff.TotalDays <= DaysThreshold;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
