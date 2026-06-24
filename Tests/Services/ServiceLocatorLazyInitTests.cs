using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class ServiceLocatorLazyInitTests
    {
        [Fact]
        public void AchievementService_lazy_factory_does_not_require_reflection_parameterless_ctor()
        {
            AchievementService service = ServiceLocator.AchievementService;
            Assert.NotNull(service);
        }

        [Fact]
        public void GoldbergArtifactService_resolves_from_locator()
        {
            GoldbergArtifactService service = ServiceLocator.GoldbergArtifactService;
            Assert.NotNull(service);
        }
    }
}
