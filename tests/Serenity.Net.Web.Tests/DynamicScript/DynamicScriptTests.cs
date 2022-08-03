using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Serenity.Localization;
using Serenity.Web.Middleware;
using System.IO;
using System.Threading.Tasks;

namespace Serenity.Tests.Web
{
    public partial class DynamicScriptTests
    {
        ServiceProvider CreateServiceProviderForDynamicScriptMiddleware()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddOptions();
            serviceCollection.Configure<LocalTextPackages>(x =>
            {
                x.Add("Site", "^(Controls|Db|Dialogs|Enums|Forms|Permission|Site|Validation)\\.");
            });
            serviceCollection.AddSingleton<IDynamicScriptManager, DynamicScriptManager>();
            serviceCollection.AddSingleton<ITwoLevelCache, NullTwoLevelCache>();
            serviceCollection.AddSingleton<IMemoryCache, NullMemoryCache>();
            serviceCollection.AddSingleton<IDistributedCache, NullDistributedCache>();
            serviceCollection.AddSingleton<IPermissionService, MockPermissions>((provider) => new MockPermissions(x => true));
            serviceCollection.AddSingleton<ITextLocalizer, MockTextLocalizer>();
            serviceCollection.AddSingleton<ILocalTextRegistry, LocalTextRegistry>();
            serviceCollection.AddSingleton<IWebHostEnvironment, MockHostEnvironment>();

            return serviceCollection.BuildServiceProvider();
        }

        async Task<HttpContext> CreateAndSendRequestToMiddlewareAsync(String path)
        {
            var serviceProvider = CreateServiceProviderForDynamicScriptMiddleware();

            RequestDelegate next = (HttpContext hc) => Task.CompletedTask;

            var middleware = new DynamicScriptMiddleware(next);

            HttpContext context = new DefaultHttpContext();

            context.Response.Body = new MemoryStream();

            context.Request.Path = path;

            context.RequestServices = serviceProvider;

            await middleware.Invoke(context);

            return context;
        }
    }
}
