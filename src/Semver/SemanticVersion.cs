using System;

namespace Acklann.Semver
{
	/// <summary>
	/// Represents a semantic version number as specified at https://semver.org/.
	/// </summary>
	public readonly struct SemanticVersion : IEquatable<SemanticVersion>, IComparable<SemanticVersion>, IFormattable
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SemanticVersion" /> struct.
		/// </summary>
		/// <param name="major">The major number.</param>
		/// <param name="minor">The minor number.</param>
		/// <param name="patch">The patch number.</param>
		public SemanticVersion(ushort major, ushort minor, ushort patch) : this(major, minor, patch, null, null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SemanticVersion" /> struct.
		/// </summary>
		/// <param name="major">The major number. (should be positive)</param>
		/// <param name="minor">The minor number. (should be positive)</param>
		/// <param name="patch">The patch number. (should be positive)</param>
		/// <param name="preRelease">The pre release.</param>
		/// <param name="build">The build.</param>
		/// <param name="strict"><c>true</c> if the arguments should be validated; otherwise <c>false</c>.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// When <paramref name="major" />, <paramref name="minor" /> or <paramref name="patch" /> is
		/// less than zero while <paramref name="strict" /> is set to true.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// When <paramref name="preRelease" /> or <paramref name="build" /> do not the valid pattern
		/// (([0-9A-Za-z-]+\.?)+), while <paramref name="strict" /> is set to <c>true</c>.
		/// </exception>
		public SemanticVersion(int major, int minor, int patch, string preRelease, string build = null, bool strict = false)
		{
			if ((_major = major) < 0 && strict) throw new ArgumentOutOfRangeException(nameof(major), string.Format(OVERFLOW_ERROR, major));
			if ((_minor = minor) < 0 && strict) throw new ArgumentOutOfRangeException(nameof(minor), string.Format(OVERFLOW_ERROR, minor));
			if ((_patch = patch) < 0 && strict) throw new ArgumentOutOfRangeException(nameof(patch), string.Format(OVERFLOW_ERROR, patch));

			if (strict)
			{
				const string rule = "it must only contain alphanumeric characters";
				var regex = new System.Text.RegularExpressions.Regex(ALPHANUMERIC_IDENTIFIERS);
				if (regex.IsMatch(preRelease) == false) throw new ArgumentOutOfRangeException(nameof(preRelease), string.Format(FORMAT_ERROR, rule));
				if (regex.IsMatch(build) == false) throw new ArgumentOutOfRangeException(nameof(build), string.Format(FORMAT_ERROR, rule));
			}

			_preRelease = preRelease;
			_build = build;
		}

		/// <summary>
		/// Gets the number for when you make incompatible API changes.
		/// </summary>
		public int Major { get => _major; }

		/// <summary>
		/// Gets the number for when you add functionality in a backwords compatible manner.
		/// </summary>
		public int Minor { get => _minor; }

		/// <summary>
		/// Gets the number for when you make backwards compatible bug fixes.
		/// </summary>
		public int Patch { get => _patch; }

		/// <summary>
		/// Gets the pre-release tag.
		/// </summary>
		public string PreRelease { get => _preRelease; }

		/// <summary>
		/// Gets the build tag.
		/// </summary>
		public string Build { get => _build; }

		/// <summary>
		/// Gets a value indicating whether this instance is a pre-release.
		/// </summary>
		/// <value><c>true</c> if this instance is a pre-release; otherwise, <c>false</c>.</value>
		public bool IsPreRelease
		{
			get => string.IsNullOrEmpty(_preRelease) == false;
		}

		/// <summary>
		/// Gets a value indicating whether this instance is stable.
		/// </summary>
		/// <value><c>true</c> if this instance is stable; otherwise, <c>false</c>.</value>
		public bool IsStable
		{
			get => (_major > 0 && IsPreRelease == false);
		}

		/// <summary>
		/// Converts the string representation of a version number to its
		/// <see cref="SemanticVersion" /> equivalent.
		/// </summary>
		/// <param name="text">A string that contains a version number to convert.</param>
		/// <param name="strict"><c>true</c> if the components should be validated; otherwise <c>false</c>.</param>
		/// <exception cref="FormatException">When an illegal character is found.</exception>
		public static SemanticVersion Parse(string text, bool strict = false)
		{
			if (string.IsNullOrEmpty(text)) return new SemanticVersion();

			char c;
			int dot1 = -1, dot2 = -1, hypen = -1, plus = -1, n = text.Length;
			for (int i = 0; i < n; i++)
			{
				switch (c = text[i])
				{
					case '.':
						if (dot1 == -1) dot1 = i;
						else if (dot2 == -1) dot2 = i;
						break;

					case '-': if (hypen == -1) hypen = i; break;
					case '+': if (plus == -1) plus = i; break;

					default:
						if (!char.IsLetterOrDigit(c)) throw new FormatException(string.Format(FORMAT_ERROR, $"'{c}' is an illegal character."));
						break;
				}
				if (plus > 0) break;
			}

			return new SemanticVersion(
				int.Parse(text.Substring(0, dot1)),
				int.Parse(text.Substring((dot1 + 1), (dot2 - dot1 - 1))),
				int.Parse(text.Substring((dot2 + 1), ((hypen > 0 ? hypen : (plus > 0 ? plus : n)) - (dot2 + 1)))),
				(hypen == -1 ? null : text.Substring((hypen + 1), (plus > 0 ? (plus - hypen - 1) : (n - hypen - 1)))),
				(plus == -1 ? null : text.Substring((plus + 1), (n - plus - 1))),
				strict
				);
		}

		/// <summary>
		/// Converts the string representation of a version number to its
		/// <see cref="SemanticVersion" /> equivalent and returns a value that indicates whether the
		/// conversion succeeded.
		/// </summary>
		/// <param name="text">A string that contains a version number to convert.</param>
		/// <param name="strict"><c>true</c> if the components should be validated; otherwise <c>false</c>.</param>
		/// <param name="result">
		/// The <see cref="SemanticVersion" /> equivalent of to the version number contained in
		/// <paramref name="text" />, if the conversion succeeds.
		/// </param>
		/// <returns>
		/// <c>true</c> if the <paramref name="text" /> parameter was converted successfully;
		/// otherwise, <c>false</c>.
		/// </returns>
		public static bool TryParse(string text, bool strict, out SemanticVersion result)
		{
			try
			{
				result = Parse(text, strict);
				return true;
			}
			catch
			{
				result = new SemanticVersion();
				return false;
			}
		}

		/// <summary>
		/// Converts the string representation of a version number to its
		/// <see cref="SemanticVersion" /> equivalent and returns a value that indicates whether the
		/// conversion succeeded.
		/// </summary>
		/// <param name="text">A string that contains a version number to convert.</param>
		/// <param name="result">
		/// The <see cref="SemanticVersion" /> equivalent of to the version number contained in
		/// <paramref name="text" />, if the conversion succeeds.
		/// </param>
		/// <returns>
		/// <c>true</c> if the <paramref name="text" /> parameter was converted successfully;
		/// otherwise, <c>false</c>.
		/// </returns>
		public static bool TryParse(string text, out SemanticVersion result) => TryParse(text, true, out result);

		/// <summary>
		/// Determines whether the specified text can be converted to a
		/// <see cref="SemanticVersion" /> object.
		/// </summary>
		/// <param name="text">A <see cref="SemanticVersion" /> string representation.</param>
		/// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
		/// <seealso cref="Parse(string, bool)" />
		public static bool IsWellFormed(string text)
		{
			if (string.IsNullOrEmpty(text)) return false;
			return System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), VALID_SEMVER);
		}

		/// <summary>
		/// Determines whether this instance adhere to the specifications outlined at https://semver.org/.
		/// </summary>
		/// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
		public bool IsWellFormed()
		{
			if (_major < 0 || _minor < 0 || _patch < 0) return false;

			var regex = new System.Text.RegularExpressions.Regex(ALPHANUMERIC_IDENTIFIERS);
			if (!string.IsNullOrEmpty(_preRelease) && !regex.IsMatch(_preRelease)) return false;
			else if (!string.IsNullOrEmpty(_build) && !regex.IsMatch(_build)) return false;
			else return true;
		}

		/// <summary>
		/// Returns a new <see cref="SemanticVersion" /> in which the <see cref="Major" /> number is
		/// incremented in accordance to https://semver.org/#spec-item-8.
		/// </summary>
		/// <param name="value">A new major value.</param>
		/// <param name="preRelease">A new pre-release value.</param>
		/// <param name="build">A new build value.</param>
		public SemanticVersion NextMajor(int value = default, string preRelease = null, string build = null)
		{
			return new SemanticVersion((value == default ? (_major + 1) : value), 0, 0, (preRelease ?? _preRelease), (build ?? _build));
		}

		/// <summary>
		/// Returns a new <see cref="SemanticVersion" /> in which the <see cref="Minor" /> number is
		/// incremented in accordance to https://semver.org/#spec-item-7.
		/// </summary>
		/// <param name="value">A new minor value.</param>
		/// <param name="preRelease">A new pre-release value.</param>
		/// <param name="build">A new build value.</param>
		public SemanticVersion NextMinor(int value = default, string preRelease = null, string build = null)
		{
			return new SemanticVersion(_major, (value == default ? (_minor + 1) : value), 0, (preRelease ?? _preRelease), (build ?? _build));
		}

		/// <summary>
		/// Returns a new <see cref="SemanticVersion" /> in which the <see cref="Patch" /> number is
		/// incremented in accordance to https://semver.org/#spec-item-6.
		/// </summary>
		/// <param name="value">A new patch value.</param>
		/// <param name="preRelease">A new pre-release value.</param>
		/// <param name="build">A new build value.</param>
		public SemanticVersion NextPatch(int value = default, string preRelease = null, string build = null)
		{
			return new SemanticVersion(_major, _minor, (value == default ? (_patch + 1) : value), (preRelease ?? _preRelease), (build ?? _build));
		}

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <returns>A <see cref="System.String" /> that represents this instance.</returns>
		public override string ToString()
		{
			return string.Concat(
			   _major, '.', _minor, '.', _patch,
			   (string.IsNullOrEmpty(_preRelease) ? null : "-"), _preRelease,
			   (string.IsNullOrEmpty(_build) ? null : "+"), _build);
		}

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <param name="format">A standard or custom version format string.</param>
		/// <returns>A <see cref="System.String" /> that represents this instance.</returns>
		public string ToString(string format)
		{
			return ToString(string.Concat("{0:", (string.IsNullOrEmpty(format) ? "G" : format), "}"), new SemanticVersionFormatter());
		}

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <param name="format">A standard or custom version format string.</param>
		/// <param name="formatProvider">The format provider.</param>
		/// <returns>A <see cref="System.String" /> that represents this instance.</returns>
		public string ToString(string format, IFormatProvider formatProvider)
		{
			if (string.IsNullOrEmpty(format)) format = ("{0:G}");
			if (formatProvider == null) formatProvider = new SemanticVersionFormatter();
			return string.Format(formatProvider, format, this);
		}

		#region IEquatable

		/// <summary>
		/// Determines whether two specified <see cref="SemanticVersion" /> objects have the same value.
		/// </summary>
		/// <param name="a">The first <see cref="SemanticVersion" /> to compare.</param>
		/// <param name="b">The second <see cref="SemanticVersion" /> to compare.</param>
		/// <returns>
		/// <c>true</c> if <paramref name="a" /> is the same as <paramref name="b" />; otherwise, false.
		/// </returns>
		public static bool Equals(SemanticVersion a, SemanticVersion b)
		{
			return
				a._major == b._major && a._minor == b._minor && a._patch == b._patch
				&& a._preRelease == b._preRelease;
		}

		/// <summary>
		/// Determines whether two specified <see cref="SemanticVersion" /> objects have the same value.
		/// </summary>
		/// <param name="a">The first <see cref="SemanticVersion" /> to compare.</param>
		/// <param name="b">The second <see cref="SemanticVersion" /> to compare.</param>
		/// <returns>
		/// <c>true</c> if <paramref name="a" /> is the same as <paramref name="b" />; otherwise, false.
		/// </returns>
		public static bool operator ==(SemanticVersion a, SemanticVersion b) => Equals(a, b);

		/// <summary>
		/// Determines whether two specified <see cref="SemanticVersion" /> objects do not have the
		/// same value.
		/// </summary>
		/// <param name="a">The first <see cref="SemanticVersion" /> to compare.</param>
		/// <param name="b">The second <see cref="SemanticVersion" /> to compare.</param>
		/// <returns>
		/// <c>true</c> if <paramref name="a" /> is the same as <paramref name="b" />; otherwise, false.
		/// </returns>
		public static bool operator !=(SemanticVersion a, SemanticVersion b) => !Equals(a, b);

		/// <summary>
		/// Determines whether two specified <see cref="SemanticVersion" /> objects do not have the
		/// </summary>
		/// <param name="other">An object to compare with this object.</param>
		/// <returns>
		/// <c>true</c> if the current object is equal to the <paramref name="other">other</paramref>
		/// parameter; otherwise, false.
		/// </returns>
		public bool Equals(SemanticVersion other) => Equals(this, other);

		/// <summary>
		/// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
		/// <returns>
		/// <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance;
		/// otherwise, <c>false</c>.
		/// </returns>
		public override bool Equals(object obj)
		{
			return (obj is SemanticVersion ? Equals(this, (SemanticVersion)obj) : false);
		}

		/// <summary>
		/// Returns a hash code for this instance.
		/// </summary>
		/// <returns>
		/// A hash code for this instance, suitable for use in hashing algorithms and data structures
		/// like a hash table.
		/// </returns>
		public override int GetHashCode()
		{
			return _major.GetHashCode() ^ _minor.GetHashCode() ^ _patch.GetHashCode() ^ (_preRelease?.GetHashCode() ?? 0);
		}

		#endregion IEquatable

		#region IComparable

		/// <summary>
		/// Determines whether one specified <see cref="SemanticVersion" /> has precedence over
		/// another specified <see cref="SemanticVersion" />.
		/// </summary>
		/// <param name="a">The first value.</param>
		/// <param name="b">The second value.</param>
		/// <returns>
		/// -1 if this <paramref name="a" /> is lesser, 1 if greater, 0 if the same as <paramref name="b" />.
		/// </returns>
		public static int Compare(in SemanticVersion a, in SemanticVersion b)
		{
			// documentation: https://semver.org/#spec-item-11

			if (a.Major != b.Major)
				return (a._major < b._major ? -1 : 1);
			else if (a._minor != b._minor)
				return (a._minor < b._minor ? -1 : 1);
			else if (a._patch != b._patch)
				return (a._patch < b._patch ? -1 : 1);
			else if (a._preRelease != b._preRelease)
			{
				if (string.IsNullOrEmpty(a._preRelease) && !string.IsNullOrEmpty(b._preRelease)) return 1;
				else if (!string.IsNullOrEmpty(a._preRelease) && string.IsNullOrEmpty(b._preRelease)) return -1;
				else
				{
					string[] x_identifiers = a._preRelease.Split('.'), y_identifiers = b._preRelease.Split('.');
					int xLen = x_identifiers.Length, yLen = y_identifiers.Length, intX, intY;
					string strX, strY;

					for (int i = 0; i < xLen; i++)
					{
						strX = x_identifiers[i];
						strY = ((i < yLen) ? y_identifiers[i] : null);

						if (strX == strY) continue;
						else if (numeric(x_identifiers[i], ((i < yLen) ? y_identifiers[i] : null), out intX, out intY))
							return intX < intY ? -1 : 1;
						else
							return string.Compare(strX, strY);
					}

					return xLen < yLen ? -1 : 1;
				}
			}

			return 0;

			#region Functions

			bool numeric(string c, string d, out int int1, out int int2)
			{
				int2 = 0;
				return int.TryParse(c, out int1) && int.TryParse(d, out int2);
			}

			#endregion Functions
		}

		/// <summary>
		/// Determines whether one specified <see cref="SemanticVersion" /> is less than another
		/// specified <see cref="SemanticVersion" />.
		/// </summary>
		/// <param name="a">The first value.</param>
		/// <param name="b">The second value.</param>
		/// <returns>
		/// <c>true</c> if <paramref name="a" /> is less than <paramref name="b" />; otherwise, <c>false</c>.
		/// </returns>
		public static bool operator <(SemanticVersion a, SemanticVersion b)
		{
			return Compare(a, b) == -1;
		}

		/// <summary>
		/// Determines whether one specified <see cref="SemanticVersion" /> is less or equal to
		/// another specified <see cref="SemanticVersion" />.
		/// </summary>
		/// <param name="a">The first value.</param>
		/// <param name="b">The second value.</param>
		/// <returns>
		/// <c>true</c> if <paramref name="a" /> is less or equal to <paramref name="b" />;
		/// otherwise, <c>false</c>.
		/// </returns>
		public static bool operator <=(SemanticVersion a, SemanticVersion b)
		{
			return Compare(a, b) <= 0;
		}

		/// <summary>
		/// Determines whether one specified <see cref="SemanticVersion" /> is greater than another
		/// specified <see cref="SemanticVersion" />.
		/// </summary>
		/// <param name="a">The first value.</param>
		/// <param name="b">The second value.</param>
		/// <returns>
		/// <c>true</c> if <paramref name="a" /> is greater than <paramref name="b" />; otherwise, <c>false</c>.
		/// </returns>
		public static bool operator >(SemanticVersion a, SemanticVersion b)
		{
			return Compare(a, b) == 1;
		}

		/// <summary>
		/// Determines whether one specified <see cref="SemanticVersion" /> is greater or equal to
		/// another specified <see cref="SemanticVersion" />.
		/// </summary>
		/// <param name="a">The first value.</param>
		/// <param name="b">The second value.</param>
		/// <returns>
		/// <c>true</c> if <paramref name="a" /> is greater or equal to <paramref name="b" />;
		/// otherwise, <c>false</c>.
		/// </returns>
		public static bool operator >=(SemanticVersion a, SemanticVersion b)
		{
			return Compare(a, b) >= 0;
		}

		/// <summary>
		/// Determines whether this instance has precedence over the specified <see cref="SemanticVersion" />.
		/// </summary>
		/// <param name="other">The other value.</param>
		/// <returns>-1 if this instance is lesser, 1 if greater, 0 if the same.</returns>
		public int CompareTo(SemanticVersion other) => Compare(this, other);

		#endregion IComparable

		#region Operators

		/// <summary>
		/// Performs an implicit conversion from <see cref="System.String" /> to <see cref="SemanticVersion" />.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator SemanticVersion(string text) => Parse(text);

		/// <summary>
		/// Performs an implicit conversion from <see cref="SemanticVersion" /> to <see cref="System.String" />.
		/// </summary>
		/// <param name="obj">The object.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator string(SemanticVersion obj) => obj.ToString();

		/// <summary>
		/// Performs an explicit conversion from <see cref="SemanticVersion" /> to <see cref="Version" />.
		/// </summary>
		/// <param name="version">The version.</param>
		/// <returns>The result of the conversion.</returns>
		public static explicit operator Version(SemanticVersion version)
		{
			return new Version(version.Major, version.Minor, version.Patch);
		}

		/// <summary>
		/// Performs an explicit conversion from <see cref="Version" /> to <see cref="SemanticVersion" />.
		/// </summary>
		/// <param name="version">The version.</param>
		/// <returns>The result of the conversion.</returns>
		public static explicit operator SemanticVersion(Version version)
		{
			return new SemanticVersion(version.Major, version.Minor, version.Build, null, version.Revision.ToString());
		}

		#endregion Operators

		#region Backing Members

		private const string
			ALPHANUMERIC_IDENTIFIERS = @"([0-9A-Za-z-]+\.?)+",
			VALID_SEMVER = @"^(\d+\.){2}\d+(-([0-9A-Za-z-]+\.?)+)?(\+([0-9A-Za-z-]+\.?)+)?$",
			OVERFLOW_ERROR = "Value must be greater or equal to zero, but was {0}.",
			FORMAT_ERROR = "Value is not well-formed, {0}; visit https://semver.org/#backusnaur-form-grammar-for-valid-semver-versions for more information.";

		private readonly int _major, _minor, _patch;
		private readonly string _preRelease, _build;

		#endregion Backing Members
	}
}
