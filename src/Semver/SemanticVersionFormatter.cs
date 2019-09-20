using System;

namespace Acklann.Semver
{
	internal sealed class SemanticVersionFormatter : ICustomFormatter, IFormatProvider
	{
		public object GetFormat(Type formatType)
		{
			return formatType == typeof(ICustomFormatter) ? this : null;
		}

		public string Format(string format, object arg, IFormatProvider formatProvider)
		{
			if (arg is SemanticVersion ver)
			{
				char c;
				int n = format.Length;
				var builder = new System.Text.StringBuilder();

				for (int i = 0; i < n; i++)
				{
					switch (c = format[i])
					{
						default: builder.Append(c); break;

						case 'x': builder.Append(ver.Major); break;
						case 'y': builder.Append(ver.Minor); break;
						case 'z': builder.Append(ver.Patch); break;
						case 'p': builder.Append(ver.PreRelease); break;
						case 'b': builder.Append(ver.Build); break;

						case 'G': builder.Append(ver.ToString()); break;
						case 'C': builder.Append(ver.Major).Append('.').Append(ver.Minor).Append('.').Append(ver.Patch); break;

						case 'g':
							builder.Append(ver.Major).Append('.').Append(ver.Minor).Append('.').Append(ver.Patch);
							if (!string.IsNullOrEmpty(ver.PreRelease)) builder.Append('-').Append(ver.PreRelease);
							break;

						case '\\': /* Escape */
							if ((i + 1) < n)
								builder.Append(format[++i]);
							else
								builder.Append(c);
							break;
					}
				}

				return builder.ToString();
			}

			return GetFallbackFormat(format, arg);
		}

		private string GetFallbackFormat(string format, object arg)
		{
			if (arg is IFormattable)
				return ((IFormattable)arg).ToString(format, System.Globalization.CultureInfo.CurrentCulture);
			else if (arg != null)
				return arg.ToString();
			else
				return string.Empty;
		}
	}
}
