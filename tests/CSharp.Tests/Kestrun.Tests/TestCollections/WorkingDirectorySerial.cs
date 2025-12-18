using Xunit;

namespace KestrunTests.TestCollections;

// Some tests intentionally or incidentally affect the process current working directory (CWD).
// Because CWD is process-global, parallel test execution can cause cross-test interference
// (especially on Unix/macOS where deleting the CWD is possible and getcwd() can throw).
[CollectionDefinition("WorkingDirectorySerial", DisableParallelization = true)]
public class WorkingDirectorySerialCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and the DisableParallelization flag.
}
