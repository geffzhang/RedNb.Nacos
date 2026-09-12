using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedNb.Nacos;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Config;
using RedNb.Nacos.Lock;
using RedNb.Nacos.Administration;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Grpc.Lock;
using RedNb.Nacos.Grpc.Administration;
using RedNb.Nacos.Grpc.Ai;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;

namespace RedNb.Nacos.Grpc;

/// <summary>
/// Extension methods for adding Nacos gRPC services to DI container.
/// </summary>
public static class NacosGrpcServiceCollectionExtensions
{
    /// <summary>
    /// Adds Nacos gRPC services (config, naming, AI, lock, and maintainer).
    /// </summary>
    public static IServiceCollection AddNacosGrpc(
        this IServiceCollection services,
        Action<NacosClientOptions> configure)
    {
        var options = new NacosClientOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<INacosFactory>(sp => new NacosGrpcFactory(sp));
        services.AddSingleton<IConfigService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcConfigService>>();
            return new NacosGrpcConfigService(options, logger);
        });
        services.AddSingleton<INamingService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcNamingService>>();
            return new NacosGrpcNamingService(options, logger);
        });
        services.AddSingleton<IAiService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcAiService>>();
            return new NacosGrpcAiService(options, logger);
        });
        services.AddSingleton<ILockService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcLockService>>();
            return new NacosGrpcLockService(options, logger);
        });
        services.AddSingleton<IMaintainerService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcMaintainerService>>();
            return new NacosGrpcMaintainerService(options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds Nacos gRPC config service only.
    /// </summary>
    public static IServiceCollection AddNacosGrpcConfig(
        this IServiceCollection services,
        Action<NacosClientOptions> configure)
    {
        var options = new NacosClientOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IConfigService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcConfigService>>();
            return new NacosGrpcConfigService(options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds Nacos gRPC naming service only.
    /// </summary>
    public static IServiceCollection AddNacosGrpcNaming(
        this IServiceCollection services,
        Action<NacosClientOptions> configure)
    {
        var options = new NacosClientOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<INamingService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcNamingService>>();
            return new NacosGrpcNamingService(options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds Nacos gRPC lock service only.
    /// </summary>
    public static IServiceCollection AddNacosGrpcLock(
        this IServiceCollection services,
        Action<NacosClientOptions> configure)
    {
        var options = new NacosClientOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<ILockService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcLockService>>();
            return new NacosGrpcLockService(options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds Nacos maintainer service only.
    /// </summary>
    public static IServiceCollection AddNacosGrpcMaintainer(
        this IServiceCollection services,
        Action<NacosClientOptions> configure)
    {
        var options = new NacosClientOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IMaintainerService>(sp =>
        {
            var logger = sp.GetService<ILogger<NacosGrpcMaintainerService>>();
            return new NacosGrpcMaintainerService(options, logger);
        });

        return services;
    }
}
