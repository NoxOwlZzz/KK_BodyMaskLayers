using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = PureLogicTests.All();
            int passed = 0;
            int failed = 0;

            Console.WriteLine("KK_BodyMaskLayers pure unit tests (.NET Framework 3.5)");
            Console.WriteLine("Linked production sources only; Unity is not loaded.");
            Console.WriteLine();

            for (int i = 0; i < tests.Length; i++)
            {
                TestCase test = tests[i];
                int assertionsBefore = Check.AssertionCount;
                try
                {
                    test.Body();
                    passed++;
                    Console.WriteLine(
                        "[PASS] {0} ({1} assertions)",
                        test.Name,
                        Check.AssertionCount - assertionsBefore);
                }
                catch (Exception exception)
                {
                    failed++;
                    Console.WriteLine("[FAIL] {0}", test.Name);
                    Console.WriteLine("       {0}: {1}", exception.GetType().Name, exception.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "RESULT: {0} tests, {1} passed, {2} failed, {3} assertions",
                tests.Length,
                passed,
                failed,
                Check.AssertionCount);

            return failed == 0 ? 0 : 1;
        }
    }
}
