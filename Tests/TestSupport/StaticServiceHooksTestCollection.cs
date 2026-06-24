using Xunit;

namespace SmartGoldbergEmu.Tests.TestSupport
{
    // ServiceLocatorTestScope and HttpServiceTestScope use process-wide static overrides.
    [CollectionDefinition("StaticServiceHooks", DisableParallelization = true)]
    public sealed class StaticServiceHooksTestCollection
    {
    }
}
