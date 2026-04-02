namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;

    public class SharedNamedTestCase
    {
        public string Name { get; }
        private readonly Func<Task> _Execute;

        private SharedNamedTestCase(string name, Func<Task> execute)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public Task ExecuteAsync()
        {
            return _Execute();
        }

        public static SharedNamedTestCase CreateAsync(string name, Func<Task> execute)
        {
            return new SharedNamedTestCase(name, execute);
        }

        public static SharedNamedTestCase CreateSync(string name, Action execute)
        {
            return new SharedNamedTestCase(name, () => { execute(); return Task.CompletedTask; });
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
