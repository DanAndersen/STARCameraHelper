using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace STARCameraHelper
{
    public class StringFloatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            float parsedFloat;
            if (float.TryParse(value.ToString(), out parsedFloat))
            {
                return parsedFloat;
            }
            else
            {
                return 0.0f;
            }
        }
    }
}
