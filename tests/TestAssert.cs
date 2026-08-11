using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers.Tests
{
    internal sealed class TestFailureException : Exception
    {
        public TestFailureException(string message)
            : base(message)
        {
        }
    }

    internal sealed class TestCase
    {
        public TestCase(string name, Action body)
        {
            Name = name;
            Body = body;
        }

        public readonly string Name;
        public readonly Action Body;
    }

    internal static class Check
    {
        private static int assertionCount;

        public static int AssertionCount
        {
            get { return assertionCount; }
        }

        public static void True(bool condition, string message)
        {
            assertionCount++;
            if (!condition)
            {
                throw new TestFailureException(message);
            }
        }

        public static void False(bool condition, string message)
        {
            True(!condition, message);
        }

        public static void Null(object value, string message)
        {
            True(value == null, message);
        }

        public static void NotNull(object value, string message)
        {
            True(value != null, message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            assertionCount++;
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new TestFailureException(
                    string.Format("{0} Expected: {1}; actual: {2}.", message, expected, actual));
            }
        }

        public static void Same(object expected, object actual, string message)
        {
            True(object.ReferenceEquals(expected, actual), message);
        }

        public static void NotSame(object expected, object actual, string message)
        {
            False(object.ReferenceEquals(expected, actual), message);
        }

        public static void Contains(string expectedSubstring, string actual, string message)
        {
            assertionCount++;
            if (actual == null || actual.IndexOf(expectedSubstring, StringComparison.Ordinal) < 0)
            {
                throw new TestFailureException(
                    string.Format("{0} Expected substring: {1}; actual: {2}.", message, expectedSubstring, actual));
            }
        }

        public static void SequenceEqual<T>(IList<T> expected, IList<T> actual, string message)
        {
            assertionCount++;
            if (expected == null || actual == null)
            {
                if (expected == null && actual == null)
                {
                    return;
                }

                throw new TestFailureException(message + " One sequence is null.");
            }

            if (expected.Count != actual.Count)
            {
                throw new TestFailureException(
                    string.Format("{0} Expected count: {1}; actual count: {2}.", message, expected.Count, actual.Count));
            }

            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < expected.Count; i++)
            {
                if (!comparer.Equals(expected[i], actual[i]))
                {
                    throw new TestFailureException(
                        string.Format("{0} Difference at index {1}: expected {2}; actual {3}.", message, i, expected[i], actual[i]));
                }
            }
        }

        public static TException Throws<TException>(Action action, string message)
            where TException : Exception
        {
            assertionCount++;
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }
            catch (Exception exception)
            {
                throw new TestFailureException(
                    string.Format(
                        "{0} Expected {1}, but caught {2}: {3}",
                        message,
                        typeof(TException).FullName,
                        exception.GetType().FullName,
                        exception.Message));
            }

            throw new TestFailureException(
                string.Format("{0} Expected {1}, but no exception was thrown.", message, typeof(TException).FullName));
        }
    }
}
