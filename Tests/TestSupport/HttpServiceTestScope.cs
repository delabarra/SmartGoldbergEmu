using System;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;

namespace SmartGoldbergEmu.Tests.TestSupport
{
    internal sealed class HttpServiceTestScope : IDisposable
    {
        public FakeHttpService HttpService { get; }

        public HttpServiceTestScope()
        {
            HttpService = new FakeHttpService();
            HttpServiceFactory.SetTestFactoryForTests(_ => HttpService);
        }

        public void Dispose()
        {
            HttpServiceFactory.ClearTestFactoryForTests();
        }
    }
}
