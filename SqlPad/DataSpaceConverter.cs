using System;
using System.Globalization;

namespace SqlPad
{
	public class DataSpaceConverter : ValueConverterBase
	{
		public static readonly DataSpaceConverter Instance = new DataSpaceConverter();

		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			value == null
				? parameter?.ToString() ?? ValueNotAvailable
				: PrettyPrint(System.Convert.ToDecimal(value));

		public static string PrettyPrint(decimal bytes) => PrettyPrint(bytes, CultureInfo.CurrentCulture);

		public static string PrettyPrint(decimal bytes, CultureInfo culture)
		{
			if (bytes < 1024)
			{
				return $"{bytes.ToString(culture)} B";
			}
			
			if (bytes < 1048576)
			{
				return $"{Math.Round(bytes / 1024).ToString(culture)} kB";
			}

			if (bytes < 1073741824)
			{
				return $"{Math.Round(bytes / 1048576, 1).ToString(culture)} MB";
			}

			if (bytes < 1099511627776)
			{
				return $"{Math.Round(bytes / 1073741824, 2).ToString(culture)} GB";
			}

			if (bytes < 1125899906842624)
			{
				return $"{Math.Round(bytes / 1099511627776, 2).ToString(culture)} TB";
			}

			if (bytes < 1152921504606846976)
			{
				return $"{Math.Round(bytes / 1125899906842624, 2).ToString(culture)} PB";
			}

			if (bytes < 1180591620717411303424m)
			{
				return $"{Math.Round(bytes / 1152921504606846976, 2).ToString(culture)} EB";
			}

			if (bytes < 1208925819614629174706176m)
			{
				return $"{Math.Round(bytes / 1180591620717411303424m, 2).ToString(culture)} ZB";
			}

			return $"{Math.Round(bytes / 1208925819614629174706176m, 2).ToString(culture)} YB";
		}
	}
}
