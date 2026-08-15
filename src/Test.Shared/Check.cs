namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Lightweight, framework-agnostic assertion helpers. Touchstone treats any thrown exception
    /// as a test failure, so these helpers simply throw on a failed condition. Using this helper
    /// keeps Test.Shared free of a hard dependency on any specific unit-test framework while still
    /// providing familiar assertion semantics for the migrated white-box tests.
    /// </summary>
    public static class Check
    {
        /// <summary>Assert two values are equal.</summary>
        public static void Equal<T>(T expected, T actual, string? message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(message ?? ("Expected '" + expected + "' but got '" + actual + "'."));
        }

        /// <summary>Assert two sequences are element-wise equal.</summary>
        public static void Equal<T>(IEnumerable<T> expected, IEnumerable<T> actual, string? message = null)
        {
            if (expected == null || actual == null || !expected.SequenceEqual(actual))
                throw new Exception(message ?? ("Expected sequence [" + Format(expected) + "] but got [" + Format(actual) + "]."));
        }

        /// <summary>Assert a condition is true.</summary>
        public static void True(bool condition, string? message = null)
        {
            if (!condition) throw new Exception(message ?? "Expected condition to be true.");
        }

        /// <summary>Assert a nullable condition is true.</summary>
        public static void True(bool? condition, string? message = null)
        {
            if (condition != true) throw new Exception(message ?? "Expected condition to be true.");
        }

        /// <summary>Assert a condition is false.</summary>
        public static void False(bool condition, string? message = null)
        {
            if (condition) throw new Exception(message ?? "Expected condition to be false.");
        }

        /// <summary>Assert a value is null.</summary>
        public static void Null(object? value, string? message = null)
        {
            if (value != null) throw new Exception(message ?? "Expected null.");
        }

        /// <summary>Assert a value is not null.</summary>
        public static void NotNull(object? value, string? message = null)
        {
            if (value == null) throw new Exception(message ?? "Expected non-null.");
        }

        /// <summary>Assert a sequence is not empty.</summary>
        public static void NotEmpty<T>(IEnumerable<T> collection, string? message = null)
        {
            if (collection == null || !collection.Any())
                throw new Exception(message ?? "Expected a non-empty sequence.");
        }

        /// <summary>Assert a sequence contains exactly one element and return it.</summary>
        public static T Single<T>(IEnumerable<T> collection, string? message = null)
        {
            List<T> items = collection == null ? new List<T>() : collection.ToList();
            if (items.Count != 1)
                throw new Exception(message ?? ("Expected a single element but found " + items.Count + "."));
            return items[0];
        }

        /// <summary>Assert two references are the same instance.</summary>
        public static void Same(object expected, object actual, string? message = null)
        {
            if (!ReferenceEquals(expected, actual))
                throw new Exception(message ?? "Expected the same object reference.");
        }

        /// <summary>Assert a string contains a substring.</summary>
        public static void Contains(string expectedSubstring, string actualString, string? message = null)
        {
            if (actualString == null || !actualString.Contains(expectedSubstring))
                throw new Exception(message ?? ("Expected string to contain '" + expectedSubstring + "'."));
        }

        /// <summary>Assert a collection contains an item.</summary>
        public static void Contains<T>(T expected, IEnumerable<T> collection, string? message = null)
        {
            if (collection == null || !collection.Contains(expected))
                throw new Exception(message ?? ("Expected collection to contain '" + expected + "'."));
        }

        /// <summary>Assert a collection contains an item matching a predicate.</summary>
        public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
        {
            if (collection == null || !collection.Any(predicate))
                throw new Exception(message ?? "Expected collection to contain a matching element.");
        }

        /// <summary>Assert a collection contains no item matching a predicate.</summary>
        public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
        {
            if (collection != null && collection.Any(predicate))
                throw new Exception(message ?? "Expected collection to contain no matching element.");
        }

        /// <summary>Assert every element satisfies an assertion.</summary>
        public static void All<T>(IEnumerable<T> collection, Action<T> assertion)
        {
            if (collection == null) throw new Exception("Expected a non-null collection.");
            foreach (T item in collection) assertion(item);
        }

        /// <summary>Assert an awaited action throws the exact exception type and return it.</summary>
        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException expected)
            {
                return expected;
            }
            catch (Exception other)
            {
                throw new Exception("Expected " + typeof(TException).Name + " but got " + other.GetType().Name + ": " + other.Message);
            }

            throw new Exception("Expected " + typeof(TException).Name + " but no exception was thrown.");
        }

        /// <summary>Assert an awaited action throws the given exception type or a derived type.</summary>
        public static async Task<TException> ThrowsAnyAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException expected)
            {
                return expected;
            }
            catch (Exception other)
            {
                throw new Exception("Expected " + typeof(TException).Name + " but got " + other.GetType().Name + ": " + other.Message);
            }

            throw new Exception("Expected " + typeof(TException).Name + " but no exception was thrown.");
        }

        private static string Format<T>(IEnumerable<T>? values)
        {
            return values == null ? "<null>" : string.Join(", ", values);
        }
    }
}
