using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Collections.Generic;

namespace Acklann.Semver.Tests
{
    [TestClass]
    public class SemanticVersionTest
    {
        [TestMethod]
        public void Should_override_equality_operators()
        {
            var a = new SemanticVersion(0, 0, 1);
            var b = new SemanticVersion(1, 2, 3);
            var c = new SemanticVersion(0, 0, 1);

            // Equality

            (a == c).ShouldBeTrue();
            (a == b).ShouldBeFalse();
            (a != b).ShouldBeTrue();

            a.Equals(c).ShouldBeTrue();
            a.Equals(0).ShouldBeFalse();
            a.Equals(null).ShouldBeFalse();

            a.GetHashCode().ShouldBe(c.GetHashCode());
            a.GetHashCode().ShouldNotBe(b.GetHashCode());

            // Comparison
            (a < b).ShouldBeTrue();
            (b < a).ShouldBeFalse();

            (a <= c).ShouldBeTrue();
            (b <= a).ShouldBeFalse();

            (b > a).ShouldBeTrue();
            (a > b).ShouldBeFalse();

            (a >= c).ShouldBeTrue();
            (a >= b).ShouldBeFalse();
        }

        [TestMethod]
        public void Can_increment_numbers()
        {
            var ver = new SemanticVersion(0, 0, 0);

            /// source: https://semver.org/#spec-item-6
            (ver = ver.NextPatch()).ShouldBe(new SemanticVersion(0, 0, 1));
            (ver = ver.NextPatch(5)).ShouldBe(new SemanticVersion(0, 0, 5));

            /// source: https://semver.org/#spec-item-7
            (ver = ver.NextMinor()).ShouldBe(new SemanticVersion(0, 1, 0));
            (ver = ver.NextMinor(5)).ShouldBe(new SemanticVersion(0, 5, 0));
            (ver = ver.NextPatch()).ShouldBe(new SemanticVersion(0, 5, 1));

            /// source: https://semver.org/#spec-item-8
            (ver = ver.NextMajor()).ShouldBe(new SemanticVersion(1, 0, 0));
            (ver = ver.NextMajor(5)).ShouldBe(new SemanticVersion(5, 0, 0));
            (ver = ver.NextMinor()).ShouldBe(new SemanticVersion(5, 1, 0));
            (ver = ver.NextPatch()).ShouldBe(new SemanticVersion(5, 1, 1));
        }

        [TestMethod]
        public void Can_return_custom_format()
        {
            const int x = 1, y = 23, z = 5;
            const string p = "beta", b = "81321";
            var v1 = new SemanticVersion(x, y, z);
            var v2 = new SemanticVersion(x, y, z, p, b);

            v1.ToString("g").ShouldBe($"{x}.{y}.{z}");
            v1.ToString("G").ShouldBe($"{x}.{y}.{z}");
            v2.ToString("g").ShouldBe($"{x}.{y}.{z}-{p}");
            v2.ToString("G").ShouldBe($"{x}.{y}.{z}-{p}+{b}");

            v2.ToString(null).ShouldBe($"{x}.{y}.{z}-{p}+{b}");
            v2.ToString(string.Empty).ShouldBe($"{x}.{y}.{z}-{p}+{b}");

            v2.ToString("C").ShouldBe($"{x}.{y}.{z}");
            v2.ToString("x.y.z").ShouldBe($"{x}.{y}.{z}");
            v2.ToString("x.y.z_p_b").ShouldBe($"{x}.{y}.{z}_{p}_{b}");
            v2.ToString("n\\xt: \\x.\\y.\\z\\").ShouldBe($"nxt: x.y.z\\");

            string.Format("{0}", v2).ShouldBe(v2.ToString());
        }

        [DataTestMethod]
        [DataRow("", 0, 0, 0, null, null)]
        [DataRow(null, 0, 0, 0, null, null)]
        [DataRow("1.2.3", 1, 2, 3, null, null)]
        [DataRow("0.0.1-beta", 0, 0, 1, "beta", null)]
        [DataRow("0.0.1+1234", 0, 0, 1, null, "1234")]
        [DataRow("0.0.1-beta+sha64", 0, 0, 1, "beta", "sha64")]
        [DataRow("0.0.1+sha64-beta", 0, 0, 1, null, "sha64-beta")]
        [DataRow("0.0.1-beta-beta.1+sha-64.1", 0, 0, 1, "beta-beta.1", "sha-64.1")]
        public void Can_parse_string(string input, int major, int minor, int patch, string tag, string build)
        {
            var semver = SemanticVersion.Parse(input);

            semver.Major.ShouldBe(major);
            semver.Minor.ShouldBe(minor);
            semver.Patch.ShouldBe(patch);
            semver.PreRelease.ShouldBe(tag);
            semver.Build.ShouldBe(build);
            semver.IsWellFormed().ShouldBeTrue();
        }

        [DataTestMethod]
        [DataRow("1.1.1", true)]
        [DataRow("-1.0.0", false)]
        [DataRow("1.1.1-beta", true)]
        [DataRow("1.1.1-beta;1", false)]
        [DataRow("1.1.1-beta+sha256", true)]
        [DataRow("1.1-beta+sha256.12", false)]
        [DataRow("1.1-beta++sha256.12", false)]
        public void Can_determine_if_version_string_is_well_formed(string input, bool expected)
        {
            SemanticVersion.IsWellFormed(input).ShouldBe(expected);
        }

        [DataTestMethod]
        [DataRow("0.0.1++sha256")]
        public void Should_throw_when_parsing_invalid_character(string text)
        {
            Should.Throw<Exception>(() => { SemanticVersion.Parse(text, true); });
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPrecedenceData), DynamicDataSourceType.Method)]
        public void Can_determine_version_precedence(string x, string y, int expectedValue)
        {
            SemanticVersion.Compare(x, y).ShouldBe(expectedValue);
        }

        private static IEnumerable<object[]> GetPrecedenceData()
        {
            yield return new object[] { "10.2.1", "2.3.5", 1 };

            /// source: https://semver.org/#spec-item-10

            var spec10 = new string[] { "1.0.0", "1.0.0+20130313144700", "1.0.0+exp.sha.5114f85" };
            for (int i = 1; i < spec10.Length; i++)
                yield return new object[] { spec10[i - 1], spec10[i], 0 };

            /// source: https://semver.org/#spec-item-11

            var spec11__example_1 = new string[] { "1.0.0", "2.0.0", "2.1.0", "2.1.1" };
            for (int i = 1; i < spec11__example_1.Length; i++)
                yield return new object[] { spec11__example_1[i - 1], spec11__example_1[i], -1 };

            var spec11__example_2 = new string[] { "1.0.0-alpha", "1.0.0" };
            for (int i = 1; i < spec11__example_2.Length; i++)
                yield return new object[] { spec11__example_2[i - 1], spec11__example_2[i], -1 };

            var spec11__example_3 = new string[] {
                "1.0.0-alpha", "1.0.0-alpha.1",
                "1.0.0-alpha.beta", "1.0.0-beta",
                "1.0.0-beta.2", "1.0.0-beta.11",
                "1.0.0-rc.1", "1.0.0"
            };
            for (int i = 1; i < spec11__example_3.Length; i++)
                yield return new object[] { spec11__example_3[i - 1], spec11__example_3[i], -1 };
        }
    }
}