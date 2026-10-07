using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dav.AspNetCore.Server.Tests;

public class WebDavStartupValidationTest
{
    [Fact]
    public async Task Development_Anonymous_IsAllowed()
    {
        var validation = Create(new WebDavOptions(), isDevelopment: true);

        await validation.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Production_AnonymousWithoutOptIn_Throws()
    {
        var validation = Create(new WebDavOptions(), isDevelopment: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => validation.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Production_AnonymousWithOptIn_IsAllowed()
    {
        var validation = Create(new WebDavOptions { AllowAnonymousAccess = true }, isDevelopment: false);

        await validation.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Production_RequiresAuthenticationWithScheme_IsAllowed()
    {
        var validation = Create(
            new WebDavOptions { RequiresAuthentication = true },
            isDevelopment: false,
            registerScheme: true);

        await validation.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Production_RequiresAuthenticationWithoutScheme_Throws()
    {
        var validation = Create(
            new WebDavOptions { RequiresAuthentication = true },
            isDevelopment: false,
            registerScheme: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => validation.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RequiresAuthenticationAndAllowAnonymousAccess_Throws()
    {
        var validation = Create(
            new WebDavOptions { RequiresAuthentication = true, AllowAnonymousAccess = true },
            isDevelopment: true,
            registerScheme: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => validation.StartAsync(CancellationToken.None));
    }

    [Fact]
    public void AddWebDav_RegistersStartupValidation()
    {
        var services = new ServiceCollection();

        services.AddWebDav(_ => { });

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(WebDavStartupValidation));
    }

    private static WebDavStartupValidation Create(
        WebDavOptions options,
        bool isDevelopment,
        bool registerScheme = false)
    {
        var services = new ServiceCollection();

        var environment = new Mock<IWebHostEnvironment>();
        environment
            .SetupGet(e => e.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);
        services.AddSingleton(environment.Object);

        if (registerScheme)
        {
            var schemeProvider = new Mock<IAuthenticationSchemeProvider>();
            schemeProvider
                .Setup(provider => provider.GetAllSchemesAsync())
                .ReturnsAsync(new[] { new AuthenticationScheme("Basic", "Basic", typeof(DummyAuthenticationHandler)) });
            services.AddSingleton(schemeProvider.Object);
        }

        var serviceProvider = services.BuildServiceProvider();
        return new WebDavStartupValidation(
            serviceProvider,
            options,
            NullLogger<WebDavStartupValidation>.Instance);
    }

    private sealed class DummyAuthenticationHandler : IAuthenticationHandler
    {
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
            => Task.CompletedTask;

        public Task<AuthenticateResult> AuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(AuthenticationProperties? properties)
            => Task.CompletedTask;
    }
}
