namespace RallyAPI.Integration.Tests.Infrastructure;

/// <summary>
/// xUnit collection that shares a single IntegrationTestFactory instance
/// across all test classes, so containers start only once per test run.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationTestFactory>
{
}
