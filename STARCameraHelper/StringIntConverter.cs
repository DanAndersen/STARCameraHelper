using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace STARCameraHelper
{
    public class StringIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            int parsedInt;
            if (Int32.TryParse(value.ToString(), out parsedInt))
            {
                return parsedInt;
            }
            else
            {
                return 0;
            }
        }
    }
}
