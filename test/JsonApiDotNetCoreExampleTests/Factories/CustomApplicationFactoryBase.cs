using System;
using JsonApiDotNetCoreExample;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;

namespace JsonApiDotNetCoreExampleTests
{
    public class CustomApplicationFactoryBase : WebApplicationFactory<TestStartup>, IApplicationFactory
    {
        public readonly HttpClient Client;
        private readonly IServiceScope _scope;

        public IServiceProvider ServiceProvider => _scope.ServiceProvider;

        public CustomApplicationFactoryBase()
        {
            Client = CreateClient();
            _scope = Services.CreateScope();
        }

        public T GetService<T>() => (T)_scope.ServiceProvider.GetService(typeof(T));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseStartup<TestStartup>();
        }
    }

    public interface IApplicationFactory
    {
        IServiceProvider ServiceProvider { get; }

        T GetService<T>();
        HttpClient CreateClient();
    }
}
