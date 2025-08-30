using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace YTP.WindowsUI
{
    public class BoolToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isPaused = false;
            if (value is bool b) isPaused = b;
            return isPaused ? SymbolRegular.Play12 : SymbolRegular.Pause12;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SymbolRegular s)
            {
                return s == SymbolRegular.Play24;
            }
            return false;
        }
    }
}
