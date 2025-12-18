using Xunit;

namespace KestrunTests.TestCollections;

[CollectionDefinition("WorkingDirectorySerial", DisableParallelization = true)]
public class WorkingDirectorySerialCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and the DisableParallelization flag.
}
