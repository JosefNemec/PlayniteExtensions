﻿using Playnite.SDK;
using Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;

namespace SteamLibrary
{
    public class BackgroundSourceToStringConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var source = (BackgroundSource)value;
            switch (source)
            {
                case BackgroundSource.Image:
                    return ResourceProvider.GetString("LOCSteamBackgroundSourceImage");
                case BackgroundSource.StoreScreenshot:
                    return ResourceProvider.GetString("LOCSteamBackgroundSourceScreenshot");
                case BackgroundSource.StoreBackground:
                    return ResourceProvider.GetString("LOCSteamBackgroundSourceStore");
                case BackgroundSource.Banner:
                    return ResourceProvider.GetString("LOCSteamBackgroundSourceBanner");
                default:
                    return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class StringListToStringConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is List<string> list)
                return string.Join("\r\n", list);

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string str)
            {
                if (str.IsNullOrWhiteSpace())
                    return null;

                return str.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            return null;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}