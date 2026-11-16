
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Orders.Api.Hubs;

namespace Orders.Tests.Integration;

public class OrdersHubIntegrationTests
{
    [Fact]
    public async Task Buyer_ShouldReceiveNotification_ForOwnGroup()
    {
        using var server = CreateServer();

        var buyerId = "buyer-123";
        var received = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = CreateConnection(server, "buyer-token");

        connection.On<string>(
            "OrderStatusChanged",
            status => received.TrySetResult(status));

        try
        {
            await connection.StartAsync();

            var client = server.CreateClient();

            var response = await client.PostAsync(
                $"/notify/{buyerId}/Confirmed",
                null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var status = await received.Task.WaitAsync(
                TimeSpan.FromSeconds(10));

            Assert.Equal("Confirmed", status);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Seller_ShouldNotConnect_ToBuyerHub()
    {
        using var server = CreateServer();

        var connection = CreateConnection(server, "seller-token");

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(
                () => connection.StartAsync());
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Buyer_ShouldNotReceiveNotification_ForAnotherBuyer()
    {
        using var server = CreateServer();

        var received = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = CreateConnection(server, "buyer-token");

        connection.On<string>(
            "OrderStatusChanged",
            _ => received.TrySetResult(true));

        try
        {
            await connection.StartAsync();

            var client = server.CreateClient();

            var response = await client.PostAsync(
                "/notify/another-buyer/Confirmed",
                null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await Task.Delay(500);

            Assert.False(received.Task.IsCompleted);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    private static TestServer CreateServer()
    {
        return new TestServer(
            new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSignalR();

                    services
                        .AddAuthentication("Test")
                        .AddScheme<
                            AuthenticationSchemeOptions,
                            TestAuthenticationHandler>(
                            "Test",
                            _ => { });

                    services.AddAuthorization();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHub<OrdersHub>("/hubs/orders");

                        endpoints.MapPost(
                            "/notify/{buyerId}/{status}",
                            async context =>
                            {
                                var buyerId =
                                    context.Request.RouteValues["buyerId"]!
                                        .ToString()!;

                                var status =
                                    context.Request.RouteValues["status"]!
                                        .ToString()!;

                                var hub = context.RequestServices
                                    .GetRequiredService<
                                        IHubContext<OrdersHub>>();

                                await hub.Clients
                                    .Group(
                                        OrdersHub.GetBuyerGroupName(buyerId))
                                    .SendAsync(
                                        "OrderStatusChanged",
                                        status);

                                context.Response.StatusCode = 200;
                            });
                    });
                }));
    }

    private static HubConnection CreateConnection(
        TestServer server,
        string token)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(server.BaseAddress, "/hubs/orders"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;

                    options.HttpMessageHandlerFactory =
                        _ => server.CreateHandler();

                    options.AccessTokenProvider =
                        () => Task.FromResult<string?>(token);
                })
            .Build();
    }

    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult>
            HandleAuthenticateAsync()
        {
            var token = Request.Query["access_token"].ToString();

            if (string.IsNullOrEmpty(token))
            {
                var authorization =
                    Request.Headers.Authorization.ToString();

                if (authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase))
                {
                    token = authorization["Bearer ".Length..].Trim();
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(
                    ClaimTypes.NameIdentifier,
                    token == "buyer-token"
                        ? "buyer-123"
                        : "seller-123"),

                new(
                    ClaimTypes.Role,
                    token == "buyer-token"
                        ? "Buyer"
                        : "Seller")
            };

            var identity = new ClaimsIdentity(
                claims,
                Scheme.Name);

            var principal = new ClaimsPrincipal(identity);

            var ticket = new AuthenticationTicket(
                principal,
                Scheme.Name);

            return Task.FromResult(
                AuthenticateResult.Success(ticket));
        }
    }
}
