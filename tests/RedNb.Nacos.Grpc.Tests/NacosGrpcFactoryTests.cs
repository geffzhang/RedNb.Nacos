using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RedNb.Nacos;
using RedNb.Nacos.Ai;
using RedNb.Nacos.Config;
using RedNb.Nacos.Lock;
using RedNb.Nacos.Administration;
using RedNb.Nacos.Naming;
using RedNb.Nacos.Grpc;
using Xunit;

namespace RedNb.Nacos.Grpc.Tests;

/// <summary>
/// Verifies that every public entry point on <see cref="NacosGrpcFactory"/>
/// (sync instance methods, static async methods, static helper, and DI
/// extensions) calls <see cref="NacosClientOptions.Validate"/> before
/// constructing or registering any service. Mirrors the HTTP factory
/// (<c>tests/RedNb.Nacos.Http.Tests/NacosFactoryTests.cs</c>) in scope.
/// </summary>
public class NacosGrpcFactoryTests
{
    private static NacosClientOptions ValidOptions() => new()
    {
        ServerAddresses = "localhost:8848"
    };

    private static NacosClientOptions UsernameOnlyNoAddresses() => new()
    {
        ServerAddresses = string.Empty,
        Username = "nacos",
        Password = "nacos"
    };

    private static NacosClientOptions AkSkOnlyNoAddresses() => new()
    {
        ServerAddresses = string.Empty,
        AccessKey = "ak",
        SecretKey = "sk"
    };

    private static NacosClientOptions AllAddressesEmpty() => new()
    {
        ServerAddresses = string.Empty,
        ConsoleAddresses = string.Empty,
        Endpoint = string.Empty
    };

    #region Sync instance methods (NacosClientOptions overload)

    [Fact]
    public void CreateConfigService_ValidOptions_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateConfigService(ValidOptions());
        service.Should().NotBeNull().And.BeAssignableTo<IConfigService>();
    }

    [Fact]
    public void CreateNamingService_ValidOptions_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateNamingService(ValidOptions());
        service.Should().NotBeNull().And.BeAssignableTo<INamingService>();
    }

    [Fact]
    public void CreateAiService_ValidOptions_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateAiService(ValidOptions());
        service.Should().NotBeNull().And.BeAssignableTo<IAiService>();
    }

    [Fact]
    public void CreateLockService_ValidOptions_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateLockService(ValidOptions());
        service.Should().NotBeNull().And.BeAssignableTo<ILockService>();
    }

    [Fact]
    public void CreateMaintainerService_ValidOptions_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateMaintainerService(ValidOptions());
        service.Should().NotBeNull().And.BeAssignableTo<IMaintainerService>();
    }

    [Fact]
    public void CreateConfigService_UsernameButNoAddresses_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateConfigService(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateNamingService_UsernameButNoAddresses_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateNamingService(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateAiService_UsernameButNoAddresses_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateAiService(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateLockService_UsernameButNoAddresses_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateLockService(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateMaintainerService_UsernameButNoAddresses_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateMaintainerService(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateConfigService_AllAddressesEmpty_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateConfigService(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateNamingService_AllAddressesEmpty_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateNamingService(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateAiService_AllAddressesEmpty_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateAiService(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateLockService_AllAddressesEmpty_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateLockService(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateMaintainerService_AllAddressesEmpty_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateMaintainerService(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    #endregion

    #region Sync instance methods (string server address overload)

    [Fact]
    public void CreateConfigService_StringAddress_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateConfigService("localhost:8848");
        service.Should().NotBeNull().And.BeAssignableTo<IConfigService>();
    }

    [Fact]
    public void CreateNamingService_StringAddress_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateNamingService("localhost:8848");
        service.Should().NotBeNull().And.BeAssignableTo<INamingService>();
    }

    [Fact]
    public void CreateAiService_StringAddress_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateAiService("localhost:8848");
        service.Should().NotBeNull().And.BeAssignableTo<IAiService>();
    }

    [Fact]
    public void CreateLockService_StringAddress_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateLockService("localhost:8848");
        service.Should().NotBeNull().And.BeAssignableTo<ILockService>();
    }

    [Fact]
    public void CreateMaintainerService_StringAddress_Succeeds()
    {
        var factory = new NacosGrpcFactory();
        var service = factory.CreateMaintainerService("localhost:8848");
        service.Should().NotBeNull().And.BeAssignableTo<IMaintainerService>();
    }

    [Fact]
    public void CreateConfigService_EmptyStringAddress_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateConfigService(""));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateNamingService_EmptyStringAddress_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateNamingService(""));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateAiService_EmptyStringAddress_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateAiService(""));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateLockService_EmptyStringAddress_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateLockService(""));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateMaintainerService_EmptyStringAddress_Throws()
    {
        var factory = new NacosGrpcFactory();
        var ex = Assert.Throws<NacosException>(() => factory.CreateMaintainerService(""));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    #endregion

    #region Static async methods (NacosClientOptions overload)

    // The async overloads call InitializeAsync, which attempts a real gRPC
    // connection — there is no test server, so we assert only the
    // validation behavior (must throw NacosException(InvalidParam) for
    // invalid input BEFORE any network attempt).

    [Fact]
    public async Task CreateConfigServiceAsync_UsernameButNoAddresses_Throws()
    {
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateConfigServiceAsync(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public async Task CreateNamingServiceAsync_UsernameButNoAddresses_Throws()
    {
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateNamingServiceAsync(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public async Task CreateAiServiceAsync_UsernameButNoAddresses_Throws()
    {
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateAiServiceAsync(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public async Task CreateLockServiceAsync_UsernameButNoAddresses_Throws()
    {
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateLockServiceAsync(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public async Task CreateConfigServiceAsync_AllAddressesEmpty_Throws()
    {
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateConfigServiceAsync(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public async Task CreateNamingServiceAsync_AllAddressesEmpty_Throws()
    {
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateNamingServiceAsync(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public async Task CreateAiServiceAsync_AllAddressesEmpty_Throws()
    {
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateAiServiceAsync(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public async Task CreateAiServiceAsync_AkSkButNoServerAddresses_Throws()
    {
        // Mirrors the HTTP-side SecurityProxyTests.Validate_WithAkSkButNoServerAddresses_ThrowsInvalidParam
        // through the gRPC factory entry point: both AccessKey and SecretKey
        // set, ServerAddresses blank — must be rejected by Validate() with
        // InvalidParam before any gRPC dial attempt.
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateAiServiceAsync(AkSkOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public async Task CreateLockServiceAsync_AllAddressesEmpty_Throws()
    {
        var ex = await Assert.ThrowsAsync<NacosException>(
            () => NacosGrpcFactory.CreateLockServiceAsync(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    #endregion

    #region Static CreateMaintainerServiceStatic (HTTP-only, no async connect)

    [Fact]
    public void CreateMaintainerServiceStatic_ValidOptions_Succeeds()
    {
        var service = NacosGrpcFactory.CreateMaintainerServiceStatic(ValidOptions());
        service.Should().NotBeNull().And.BeAssignableTo<IMaintainerService>();
    }

    [Fact]
    public void CreateMaintainerServiceStatic_UsernameButNoAddresses_Throws()
    {
        var ex = Assert.Throws<NacosException>(
            () => NacosGrpcFactory.CreateMaintainerServiceStatic(UsernameOnlyNoAddresses()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void CreateMaintainerServiceStatic_AllAddressesEmpty_Throws()
    {
        var ex = Assert.Throws<NacosException>(
            () => NacosGrpcFactory.CreateMaintainerServiceStatic(AllAddressesEmpty()));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    #endregion

    #region DI extension methods

    [Fact]
    public void AddNacosGrpc_ValidOptions_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddNacosGrpc(o => o.ServerAddresses = "localhost:8848");
        var provider = services.BuildServiceProvider();
        provider.GetService<INacosFactory>().Should().NotBeNull();
        provider.GetService<IConfigService>().Should().NotBeNull();
        provider.GetService<INamingService>().Should().NotBeNull();
        provider.GetService<IAiService>().Should().NotBeNull();
        provider.GetService<ILockService>().Should().NotBeNull();
        provider.GetService<IMaintainerService>().Should().NotBeNull();
    }

    [Fact]
    public void AddNacosGrpc_UsernameButNoAddresses_Throws()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<NacosException>(() => services.AddNacosGrpc(o =>
        {
            o.ServerAddresses = string.Empty;
            o.Username = "nacos";
            o.Password = "nacos";
        }));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void AddNacosGrpc_AllAddressesEmpty_Throws()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<NacosException>(() => services.AddNacosGrpc(o =>
        {
            o.ServerAddresses = string.Empty;
            o.ConsoleAddresses = string.Empty;
            o.Endpoint = string.Empty;
        }));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void AddNacosGrpcConfig_AllAddressesEmpty_Throws()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<NacosException>(() => services.AddNacosGrpcConfig(o =>
        {
            o.ServerAddresses = string.Empty;
            o.ConsoleAddresses = string.Empty;
            o.Endpoint = string.Empty;
        }));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void AddNacosGrpcNaming_AllAddressesEmpty_Throws()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<NacosException>(() => services.AddNacosGrpcNaming(o =>
        {
            o.ServerAddresses = string.Empty;
            o.ConsoleAddresses = string.Empty;
            o.Endpoint = string.Empty;
        }));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void AddNacosGrpcLock_AllAddressesEmpty_Throws()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<NacosException>(() => services.AddNacosGrpcLock(o =>
        {
            o.ServerAddresses = string.Empty;
            o.ConsoleAddresses = string.Empty;
            o.Endpoint = string.Empty;
        }));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    [Fact]
    public void AddNacosGrpcMaintainer_AllAddressesEmpty_Throws()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<NacosException>(() => services.AddNacosGrpcMaintainer(o =>
        {
            o.ServerAddresses = string.Empty;
            o.ConsoleAddresses = string.Empty;
            o.Endpoint = string.Empty;
        }));
        ex.ErrorCode.Should().Be(NacosException.InvalidParam);
    }

    #endregion
}
