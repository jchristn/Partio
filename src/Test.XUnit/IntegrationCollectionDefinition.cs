namespace Test.XUnit
{
    using Xunit;

    [CollectionDefinition("Integration")]
    public class IntegrationCollectionDefinition : ICollectionFixture<IntegrationFixture>
    {
    }
}
