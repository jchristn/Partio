namespace Test.Shared
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Convenience factory for building Touchstone <see cref="TestCaseDescriptor"/> instances
    /// from a display name and a delegate. Test authors express a case as a name plus an
    /// asynchronous or synchronous body; this factory derives a stable, suite-unique case id
    /// and adapts the body to the runner-agnostic descriptor contract consumed by
    /// Test.Automated (CLI), Test.XUnit, and Test.Nunit.
    /// </summary>
    public static class TestCaseFactory
    {
        /// <summary>
        /// Build a descriptor from an asynchronous body.
        /// </summary>
        /// <param name="suiteId">Identifier of the owning suite.</param>
        /// <param name="name">Human-readable, suite-unique display name.</param>
        /// <param name="body">Asynchronous test body.</param>
        /// <returns>A Touchstone test case descriptor.</returns>
        public static TestCaseDescriptor Async(string suiteId, string name, Func<Task> body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return new TestCaseDescriptor(suiteId, DeriveCaseId(name), name, _ => body());
        }

        /// <summary>
        /// Build a descriptor from a synchronous body.
        /// </summary>
        /// <param name="suiteId">Identifier of the owning suite.</param>
        /// <param name="name">Human-readable, suite-unique display name.</param>
        /// <param name="body">Synchronous test body.</param>
        /// <returns>A Touchstone test case descriptor.</returns>
        public static TestCaseDescriptor Sync(string suiteId, string name, Action body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return new TestCaseDescriptor(suiteId, DeriveCaseId(name), name, _ =>
            {
                body();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Derive a compact, deterministic case id from a display name. Names are unique within
        /// a suite, so the sanitized form is unique as well.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <returns>Sanitized case id.</returns>
        private static string DeriveCaseId(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));

            StringBuilder builder = new StringBuilder(name.Length);
            bool lastWasSeparator = false;

            foreach (char character in name)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            string result = builder.ToString().Trim('_');
            return result.Length == 0 ? "case" : result;
        }
    }
}
