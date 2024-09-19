using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serenity.PropertyGrid;
using Serenity.Web;
using Serenity.Web.Middleware;

namespace Serenity.Extensions.DependencyInjection;

/// <summary>
/// Contains DI extension methods related to dynamic script services
/// </summary>
public static class DynamicScriptServiceCollectionExtensions
{
    /// <summary>
    /// Registers he default <see cref="IDynamicScriptManager"/> implementation.
    /// </summary>
    /// <param name="collection">Service collection</param>
    /// <returns></returns>
    public static IServiceCollection AddDynamicScriptManager(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.AddCaching();
        collection.AddTextRegistry();
        collection.TryAddSingleton<IDynamicScriptManager, DynamicScriptManager>();
        return collection;
    }

    /// <summary>
    /// Registers the default <see cref="IDynamicScriptManager" /> implementation
    /// in addition to the <see cref="IPropertyItemProvider"/> implementation.
    /// </summary>
    /// <param name="collection">Service collection</param>
    /// <returns></returns>
    public static IServiceCollection AddDynamicScripts(this IServiceCollection collection)
    {
        AddDynamicScriptManager(collection);
        collection.TryAddSingleton<IPropertyItemProvider, DefaultPropertyItemProvider>();
        return collection;
    }

    /// <summary>
    /// Registers the default <see cref="IFileWatcherFactory"/> implementation.
    /// </summary>
    /// <param name="collection">Service collection</param>
    /// <returns></returns>
    public static IServiceCollection AddFileWatcherFactory(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.TryAddSingleton<IFileWatcherFactory, DefaultFileWatcherFactory>();
        return collection;
    }

    /// <summary>
    /// Registers the default <see cref="IContentHashCache"/> implementation.
    /// </summary>
    /// <param name="collection">Service collection</param>
    public static IServiceCollection AddContentHashCache(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        AddFileWatcherFactory(collection);
        collection.AddOptions();
        collection.TryAddSingleton<IContentHashCache, ContentHashCache>();
        return collection;
    }

    /// <summary>
    /// Registers the default service types related to CSS bundling, including
    /// <see cref="ICssBundleManager"/>.
    /// </summary>
    /// <param name="collection">Service collection</param>
    /// <exception cref="ArgumentNullException">Collection is null</exception>
    public static void AddCssBundling(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.AddDynamicScriptManager();
        collection.AddContentHashCache();
        collection.AddEsBuildCssMinifier();
        collection.TryAddSingleton<ICssBundleManager, CssBundleManager>();
    }

    /// <summary>
    /// Registers the default service types related to CSS bundling, including
    /// <see cref="ICssBundleManager"/>.
    /// </summary>
    /// <param name="collection">Service collection</param>
    /// <param name="setupAction">Action to edit options</param>
    /// <exception cref="ArgumentNullException">Collection is null</exception>
    public static void AddCssBundling(this IServiceCollection collection,
        Action<CssBundlingOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(collection);

        ArgumentNullException.ThrowIfNull(setupAction);

        collection.AddCssBundling();
        collection.Configure(setupAction);
    }

    /// <summary>
    /// Registers the default service types related to Script bundling, including
    /// <see cref="IScriptBundleManager"/>.
    /// </summary>
    /// <param name="collection">Service collection</param>
    /// <exception cref="ArgumentNullException">Collection is null</exception>
    public static void AddScriptBundling(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.AddDynamicScriptManager();
        collection.AddContentHashCache();
        collection.AddEsBuildScriptMinifier();
        collection.TryAddSingleton<IScriptBundleManager, ScriptBundleManager>();
    }

    /// <summary>
    /// Registers the default service types related to Script bundling, including
    /// <see cref="IScriptBundleManager"/>.
    /// </summary>
    /// <param name="collection">Service collection</param>
    /// <param name="setupAction">Action to edit options</param>
    /// <exception cref="ArgumentNullException">Collection is null</exception>
    public static void AddScriptBundling(this IServiceCollection collection,
        Action<ScriptBundlingOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(collection);

        ArgumentNullException.ThrowIfNull(setupAction);

        collection.AddScriptBundling();
        collection.Configure(setupAction);
    }

    /// <summary>
    /// Adds <see cref="DynamicScriptMiddleware"/> to the application pipeline
    /// </summary>
    /// <param name="builder">Application builder</param>
    public static IApplicationBuilder UseDynamicScriptMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DynamicScriptMiddleware>();
    }

    /// <summary>
    /// Adds dynamic script related services to the application including
    /// dynamic script types, css watching, script watching, template scripts, and
    /// dynamic script middleware
    /// </summary>
    /// <param name="builder">Application builder</param>
    public static IApplicationBuilder UseDynamicScripts(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        UseDynamicScriptTypes(builder.ApplicationServices);
        UseCssWatching(builder.ApplicationServices);
        UseScriptWatching(builder.ApplicationServices);

        return UseDynamicScriptMiddleware(builder);
    }

    /// <summary>
    /// Executes registration of dynamic script types including data scripts,
    /// lookup scripts, distinct values, columns and forms.
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns></returns>
    public static IServiceProvider UseDynamicScriptTypes(this IServiceProvider serviceProvider)
    {
        var scriptManager = serviceProvider.GetRequiredService<IDynamicScriptManager>();
        var propertyProvider = serviceProvider.GetRequiredService<IPropertyItemProvider>();
        var typeSource = serviceProvider.GetRequiredService<ITypeSource>();

        DataScriptRegistration.RegisterDataScripts(scriptManager,
            typeSource, serviceProvider);

        LookupScriptRegistration.RegisterLookupScripts(scriptManager,
            typeSource, serviceProvider);

        DistinctValuesRegistration.RegisterDistinctValueScripts(scriptManager,
            typeSource, serviceProvider);

        var columnScripts = ColumnsScriptRegistration.RegisterColumnsScripts(scriptManager,
            typeSource, propertyProvider, serviceProvider);

        var formScripts = FormScriptRegistration.RegisterFormScripts(scriptManager,
            typeSource, propertyProvider, serviceProvider);

        scriptManager.Register("ColumnAndFormBundle", new ConcatenatedScript(
            [
                () => PropertyItemsScript.Compact((columnScripts as IEnumerable<PropertyItemsScript>).Concat(formScripts)
                    .Select(x => (x.ScriptName, (PropertyItemsData)x.GetScriptData())))
            ]));

        return serviceProvider;
    }

    /// <summary>
    /// Actives CSS file watching
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    public static IServiceProvider UseCssWatching(this IServiceProvider serviceProvider)
    {
        var hostEnvironment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
        var CssPaths = new[] { System.IO.Path.Combine(hostEnvironment.WebRootPath, "Content") };
        UseCssWatching(serviceProvider, CssPaths);
        return serviceProvider;
    }

    /// <summary>
    /// Activates CSS file watching
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="cssPaths">CSS paths</param>
    /// <exception cref="ArgumentNullException">serviceProvider is null</exception>
    /// <exception cref="InvalidOperationException">CSS bundle manager is not registered</exception>
    public static IServiceProvider UseCssWatching(this IServiceProvider serviceProvider,
        params string[] cssPaths)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (cssPaths == null || cssPaths.Length == 0)
            throw new ArgumentNullException(nameof(cssPaths));

        var bundleManager = serviceProvider.GetService<ICssBundleManager>();
        var contentHashCache = serviceProvider.GetService<IContentHashCache>();

        if (bundleManager == null && contentHashCache == null)
            throw new InvalidOperationException("CSS watching has no use when " +
                "there is no CSS bundle manager or content hash cache!");

        foreach (var path in cssPaths)
        {
            var fileWatcherFactory = serviceProvider.GetRequiredService<IFileWatcherFactory>();
            if (fileWatcherFactory.Watchers.Any(x =>
                    x.Path == path && x.Filter == "*.css") == true)
                continue;

            var CssWatcher = fileWatcherFactory.Create(path, "*.css");
            CssWatcher.Changed += (name) =>
            {
                bundleManager?.CssChanged();
                contentHashCache?.ScriptsChanged();
            };

            fileWatcherFactory.KeepAlive(CssWatcher);
        }

        return serviceProvider;
    }

    /// <summary>
    /// Activates script file watching
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    public static IServiceProvider UseScriptWatching(this IServiceProvider serviceProvider)
    {
        var hostEnvironment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
        var scriptPaths = new[] { System.IO.Path.Combine(hostEnvironment.WebRootPath, "Scripts") };
        UseScriptWatching(serviceProvider, scriptPaths);
        return serviceProvider;
    }

    /// <summary>
    /// Activates script file watching
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="scriptPaths">List of script paths to watch</param>
    /// <exception cref="ArgumentNullException">serviceProvider or scriptPaths is null</exception>
    /// <exception cref="InvalidOperationException">Script bundle manager is not registered</exception>
    public static IServiceProvider UseScriptWatching(this IServiceProvider serviceProvider,
        params string[] scriptPaths)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (scriptPaths == null || scriptPaths.Length == 0)
            throw new ArgumentNullException(nameof(scriptPaths));

        var bundleManager = serviceProvider.GetService<IScriptBundleManager>();
        var contentHashCache = serviceProvider.GetService<IContentHashCache>();

        if (bundleManager == null && contentHashCache == null)
            throw new InvalidOperationException("Script watching has no use when " +
                "there is no script bundle manager or content hash cache!");

        foreach (var path in scriptPaths)
        {
            var fileWatcherFactory = serviceProvider.GetRequiredService<IFileWatcherFactory>();
            if (fileWatcherFactory.Watchers.Any(x =>
                    x.Path == path && x.Filter == "*.js") == true)
                continue;

            var scriptWatcher = fileWatcherFactory.Create(path, "*.js");
            scriptWatcher.Changed += (name) =>
            {
                bundleManager?.ScriptsChanged();
                contentHashCache?.ScriptsChanged();
            };

            fileWatcherFactory.KeepAlive(scriptWatcher);
        }

        return serviceProvider;
    }

    /// <summary>
    /// Registers the default local text initializer
    /// </summary>
    /// <param name="collection">Service collection</param>
    public static void AddLocalTextInitializer(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        collection.TryAddSingleton<ILocalTextInitializer, DefaultLocalTextInitializer>();
    }


    /// <summary>
    /// Initializes local texts by calling <see cref="ILocalTextInitializer.Initialize"/>
    /// </summary>
    public static void InitializeLocalTexts(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var registry = app.ApplicationServices.GetRequiredService<ILocalTextRegistry>();
        var initializer = app.ApplicationServices.GetRequiredService<ILocalTextInitializer>();
        initializer.Initialize(registry);
    }
}
