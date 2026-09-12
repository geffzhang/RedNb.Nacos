using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedNb.Nacos;
using RedNb.Nacos.Config;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Grpc;
using RedNb.Nacos.Grpc.Config;
using RedNb.Nacos.Grpc.Naming;
using RedNb.Nacos.Grpc.Ai;

namespace RedNb.Nacos.DependencyInjection;

/// <summary>Recommended Nacos registration. Runtime Config/Naming use gRPC; AI routes by capability.</summary>
public static class NacosServiceCollectionExtensions
{
    private static void Configure(IServiceCollection services, Action<NacosClientOptions> configure)
        => services.AddOptions<NacosClientOptions>().Configure(configure)
            .Validate(options => { options.Validate(); return true; }).ValidateOnStart();

    private static NacosClientOptions Options(IServiceProvider services)
        => services.GetRequiredService<IOptions<NacosClientOptions>>().Value;

    /// <summary>Registers lazy Config and Naming services; AI and management remain opt-in.</summary>
    public static IServiceCollection AddNacos(this IServiceCollection services, Action<NacosClientOptions> configureOptions)
    {
        Configure(services, configureOptions);
        services.TryAddSingleton<INacosFactory>(sp => new NacosGrpcFactory(sp));
        RegisterConfig(services);
        RegisterNaming(services);
        return services;
    }

    private static void RegisterConfig(IServiceCollection services)
        => services.TryAddSingleton<IConfigService>(sp => new NacosGrpcConfigService(Options(sp), sp.GetService<ILogger<NacosGrpcConfigService>>()));

    private static void RegisterNaming(IServiceCollection services)
        => services.TryAddSingleton<INamingService>(sp => new NacosGrpcNamingService(Options(sp), sp.GetService<ILogger<NacosGrpcNamingService>>()));

    /// <summary>Registers only the configuration service.</summary>
    public static IServiceCollection AddNacosConfig(this IServiceCollection services, Action<NacosClientOptions> configureOptions)
    {
        Configure(services, configureOptions); RegisterConfig(services); return services;
    }

    /// <summary>Registers only the naming service.</summary>
    public static IServiceCollection AddNacosNaming(this IServiceCollection services, Action<NacosClientOptions> configureOptions)
    {
        Configure(services, configureOptions); RegisterNaming(services); return services;
    }

    /// <summary>Registers the AI facade and narrow interfaces with a shared singleton.</summary>
    public static IServiceCollection AddNacosAi(this IServiceCollection services, Action<NacosClientOptions> configureOptions)
    {
        Configure(services, configureOptions);
        services.TryAddSingleton<IAiService>(sp => new NacosAiClient(Options(sp)));
        services.TryAddSingleton<IA2aService>(sp => sp.GetRequiredService<IAiService>());
        services.TryAddSingleton<IPromptService>(sp => sp.GetRequiredService<IAiService>());
        services.TryAddSingleton<ISkillService>(sp => sp.GetRequiredService<IAiService>());
        services.TryAddSingleton<IAgentSpecService>(sp => sp.GetRequiredService<IAiService>());
        return services;
    }

    /// <summary>Registers v3 namespace administration as an opt-in capability.</summary>
    public static IServiceCollection AddNacosAdministration(this IServiceCollection services, Action<NacosClientOptions> configureOptions)
    {
        Configure(services, configureOptions);
        services.TryAddSingleton<RedNb.Nacos.Administration.IAdministrationService>(sp => new RedNb.Nacos.Http.Administration.NacosAdministrationService(Options(sp)));
        return services;
    }

    /// <summary>Registers an isolated named client. Resolve using GetRequiredKeyedService with this name.</summary>
    public static IServiceCollection AddNacos(this IServiceCollection services, string name, Action<NacosClientOptions> configureOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        services.AddOptions<NacosClientOptions>(name).Configure(configureOptions)
            .Validate(options => { options.Validate(); return true; }).ValidateOnStart();
        services.AddKeyedSingleton<IConfigService>(name, (sp, _) => new NacosGrpcConfigService(sp.GetRequiredService<IOptionsMonitor<NacosClientOptions>>().Get(name)));
        services.AddKeyedSingleton<INamingService>(name, (sp, _) => new NacosGrpcNamingService(sp.GetRequiredService<IOptionsMonitor<NacosClientOptions>>().Get(name)));
        services.AddKeyedSingleton<IAiService>(name, (sp, _) => new NacosAiClient(sp.GetRequiredService<IOptionsMonitor<NacosClientOptions>>().Get(name)));
        return services;
    }

    /// <summary>Registers the default client from explicit connection options.</summary>
    public static IServiceCollection AddNacos(this IServiceCollection services, string serverAddresses,
        string? username = null, string? password = null, string? @namespace = null)
        => services.AddNacos(options =>
        {
            options.ServerAddresses = serverAddresses; options.Username = username;
            options.Password = password; options.Namespace = @namespace ?? "";
        });
}
