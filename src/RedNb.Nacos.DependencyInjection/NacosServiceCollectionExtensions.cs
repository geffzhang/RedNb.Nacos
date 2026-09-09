using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RedNb.Nacos.Core;
using RedNb.Nacos.Core.Ai;
using RedNb.Nacos.Core.Config;
using RedNb.Nacos.Core.Naming;
using RedNb.Nacos.Client;
using RedNb.Nacos.Client.Ai;
using RedNb.Nacos.Client.Config;
using RedNb.Nacos.Client.Naming;

namespace RedNb.Nacos.DependencyInjection;

/// <summary>
/// Extension methods for registering Nacos services with dependency injection.
/// </summary>
public static class NacosServiceCollectionExtensions
{
    /// <summary>
    /// Adds Nacos HTTP client services to the service collection.
    /// Includes both Config and Naming services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Nacos client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNacos(
        this IServiceCollection services, 
        Action<NacosClientOptions> configureOptions)
    {
        services.Configure(configureOptions);
        
        services.TryAddSingleton<INacosFactory, NacosFactory>();
        
        services.TryAddSingleton<IConfigService>(sp =>
        {
            var options = new NacosClientOptions();
            configureOptions(options);
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<NacosConfigService>();
            return new NacosConfigService(options, logger);
        });

        services.TryAddSingleton<INamingService>(sp =>
        {
            var options = new NacosClientOptions();
            configureOptions(options);
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<NacosNamingService>();
            return new NacosNamingService(options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds only Nacos config service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Nacos client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNacosConfig(
        this IServiceCollection services, 
        Action<NacosClientOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.TryAddSingleton<IConfigService>(sp =>
        {
            var options = new NacosClientOptions();
            configureOptions(options);
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<NacosConfigService>();
            return new NacosConfigService(options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds only Nacos naming service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Nacos client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNacosNaming(
        this IServiceCollection services, 
        Action<NacosClientOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.TryAddSingleton<INamingService>(sp =>
        {
            var options = new NacosClientOptions();
            configureOptions(options);
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<NacosNamingService>();
            return new NacosNamingService(options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds the Nacos AI registry service to the service collection.
    /// The registered <see cref="IAiService"/> covers MCP, A2A, Prompt, Skill and AgentSpec
    /// operations; the narrower interfaces resolve to the same singleton instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Nacos client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNacosAi(
        this IServiceCollection services,
        Action<NacosClientOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.TryAddSingleton<IAiService>(sp =>
        {
            var options = new NacosClientOptions();
            configureOptions(options);
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<NacosAiService>();
            return new NacosAiService(options, logger);
        });

        services.TryAddSingleton<IPromptService>(sp => sp.GetRequiredService<IAiService>());
        services.TryAddSingleton<ISkillService>(sp => sp.GetRequiredService<IAiService>());
        services.TryAddSingleton<IAgentSpecService>(sp => sp.GetRequiredService<IAiService>());

        return services;
    }

    /// <summary>
    /// Adds Nacos client with pre-configured connection settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serverAddresses">Nacos server addresses (comma-separated).</param>
    /// <param name="username">Optional username for authentication.</param>
    /// <param name="password">Optional password for authentication.</param>
    /// <param name="namespace">Optional namespace (tenant) identifier.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNacos(
        this IServiceCollection services, 
        string serverAddresses, 
        string? username = null, 
        string? password = null, 
        string? @namespace = null)
    {
        return services.AddNacos(options =>
        {
            options.ServerAddresses = serverAddresses;
            options.Username = username;
            options.Password = password;
            options.Namespace = @namespace ?? string.Empty;
        });
    }
}
