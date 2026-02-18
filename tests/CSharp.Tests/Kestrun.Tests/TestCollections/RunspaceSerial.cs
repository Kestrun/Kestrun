using Xunit;

namespace KestrunTests.TestCollections;

[CollectionDefinition("RunspaceSerial", DisableParallelization = true)]
public class RunspaceSerial : ICollectionFixture<object>
{
    // Marker collection for tests that create/open PowerShell runspaces.
}
