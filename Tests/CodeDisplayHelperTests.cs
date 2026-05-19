using System;
using System.Reflection;

namespace TypeSunny.Tests
{
    internal static class CodeDisplayHelperTests
    {
        private static int _failures;
        private static readonly Type HelperType = ResolveHelperType();

        private static int Main()
        {
            Run("helper type is available", HelperTypeIsAvailable);
            Run("extracts trailing digit from plain code", ExtractsTrailingDigitFromPlainCode);
            Run("extracts trailing zero from plain code", ExtractsTrailingZeroFromPlainCode);
            Run("extracts trailing digit after suffix separator", ExtractsTrailingDigitAfterSuffixSeparator);
            Run("returns blank when code has no trailing digit", ReturnsBlankWhenCodeHasNoTrailingDigit);
            Run("returns blank when code is empty", ReturnsBlankWhenCodeIsEmpty);

            if (_failures == 0)
            {
                Console.WriteLine("All CodeDisplayHelper tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " CodeDisplayHelper test(s) failed.");
            return 1;
        }

        private static void HelperTypeIsAvailable()
        {
            AssertTrue(HelperType != null, "Expected CodeDisplayHelper to be available.");
        }

        private static void ExtractsTrailingDigitFromPlainCode()
        {
            AssertEqual("2", Invoke("rm2"));
        }

        private static void ExtractsTrailingZeroFromPlainCode()
        {
            AssertEqual("0", Invoke("okvivi0"));
        }

        private static void ExtractsTrailingDigitAfterSuffixSeparator()
        {
            AssertEqual("3", Invoke("abc3·说明"));
        }

        private static void ReturnsBlankWhenCodeHasNoTrailingDigit()
        {
            AssertEqual("", Invoke("abcd"));
            AssertEqual("", Invoke("zg_"));
        }

        private static void ReturnsBlankWhenCodeIsEmpty()
        {
            AssertEqual("", Invoke(""));
            AssertEqual("", Invoke(null));
        }

        private static string Invoke(string rawCode)
        {
            AssertTrue(HelperType != null, "Expected helper type to resolve before invoking.");

            var method = HelperType.GetMethod(
                "TryGetTailBadgeText",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            AssertTrue(method != null, "Expected TryGetTailBadgeText to exist.");

            return (string)method.Invoke(null, new object[] { rawCode });
        }

        private static Type ResolveHelperType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("TypeSunny.Utils.CodeDisplayHelper", false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("FAIL: " + name);
                Console.WriteLine(ex.Message);
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void AssertEqual(string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception("Expected '" + expected + "', got '" + actual + "'.");
        }
    }
}
