using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RallyAPI.Integration.Tests.Infrastructure;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.ValueObjects;
using RallyAPI.Users.Infrastructure.Persistence;
using Xunit;

namespace RallyAPI.Integration.Tests.Flows;

/// <summary>
/// Real login / switch-outlet / refresh flow against the real app, DB and token signing.
/// Guards: owner refresh works, owner-in-outlet sessions keep owner access across refresh,
/// and direct restaurant logins never get owner access.
/// </summary>
public sealed class OwnerOutletAuthFlowTests : IntegrationTestBase
{
    private const string Password = "Passw0rd!x";
    private const string OwnerEmail = "owner@flow.test";
    private const string OutletEmail = "outlet1@flow.test";

    private Guid _ownerId;
    private Guid _outlet1Id;

    public OwnerOutletAuthFlowTests(IntegrationTestFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var hash = hasher.Hash(Password);

        var owner = RestaurantOwner.Create(
            "Flow Owner", Email.Create(OwnerEmail).Value, hash, PhoneNumber.Create("9990001111").Value).Value;
        db.RestaurantOwners.Add(owner);

        Restaurant Outlet(string name, string email, string phone)
        {
            var r = Restaurant.Create(name, PhoneNumber.Create(phone).Value, Email.Create(email).Value,
                hash, "42 Brigade Road", 12.9716m, 77.5946m).Value;
            r.SetOwner(owner.Id);
            return r;
        }

        var outlet1 = Outlet("Outlet One", OutletEmail, "9990002222");
        var outlet2 = Outlet("Outlet Two", "outlet2@flow.test", "9990003333");
        db.Restaurants.AddRange(outlet1, outlet2);
        await db.SaveChangesAsync();

        _ownerId = owner.Id;
        _outlet1Id = outlet1.Id;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private record Tokens(string Access, string Refresh);

    private static JwtSecurityToken Read(string jwt) => new JwtSecurityTokenHandler().ReadJwtToken(jwt);

    private static string Claim(string jwt, string type) =>
        Read(jwt).Claims.FirstOrDefault(c => c.Type == type)?.Value ?? "";

    private async Task<Tokens> PostForTokens(string url, object? body)
    {
        var res = await Client.PostAsync(url, body is null ? null : JsonBody(body));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        return new Tokens(json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }

    private Task<Tokens> OwnerLogin() =>
        PostForTokens("/api/owners/login", new { email = OwnerEmail, password = Password });

    private Task<Tokens> RestaurantLogin() =>
        PostForTokens("/api/restaurants/login", new { email = OutletEmail, password = Password });

    private async Task<Tokens> SwitchToOutlet1(Tokens owner)
    {
        AuthenticateAs(owner.Access);
        return await PostForTokens($"/api/owners/outlets/{_outlet1Id}/switch", null);
    }

    private async Task<HttpResponseMessage> Get(string url, string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await Client.SendAsync(req);
    }

    private async Task<HttpResponseMessage> Refresh(string refreshToken)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        return await Client.PostAsync("/api/auth/refresh", JsonBody(new { refreshToken }));
    }

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OwnerRefresh_ShouldReturnNewOwnerToken()
    {
        var owner = await OwnerLogin();

        var res = await Refresh(owner.Refresh);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var newAccess = json.GetProperty("accessToken").GetString()!;
        json.GetProperty("refreshToken").GetString().Should().NotBe(owner.Refresh);
        Claim(newAccess, "user_type").Should().Be("owner");
        Claim(newAccess, "sub").Should().Be(_ownerId.ToString());
        (await Get("/api/owners/me", newAccess)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SwitchToOutlet_ShouldIssueOwnerAccessToken_ThatCallsOwnerAndRestaurantEndpoints()
    {
        var outlet = await SwitchToOutlet1(await OwnerLogin());

        Claim(outlet.Access, "user_type").Should().Be("restaurant");
        Claim(outlet.Access, "sub").Should().Be(_outlet1Id.ToString());
        Claim(outlet.Access, "owner_id").Should().Be(_ownerId.ToString());
        Claim(outlet.Access, "owner_access").Should().Be("true");

        (await Get("/api/owners/me", outlet.Access)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Get("/api/owners/me/outlets", outlet.Access)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Get("/api/restaurants/me/outlets", outlet.Access)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DirectRestaurantLogin_ShouldNotCarryOwnerAccess_AndIsDeniedOnOwnerEndpoints()
    {
        var direct = await RestaurantLogin();

        Claim(direct.Access, "owner_access").Should().BeEmpty();
        Claim(direct.Access, "owner_id").Should().BeEmpty();
        (await Get("/api/owners/me", direct.Access)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Get("/api/restaurants/me/outlets", direct.Access)).StatusCode.Should().Be(HttpStatusCode.OK);

        // ...and refreshing a direct login must not upgrade it.
        var refreshed = await Refresh(direct.Refresh);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAccess = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync())
            .RootElement.GetProperty("accessToken").GetString()!;
        Claim(newAccess, "owner_access").Should().BeEmpty();
        (await Get("/api/owners/me", newAccess)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OutletRefresh_ShouldKeepOwnerAccess_AndRotateRefreshToken()
    {
        var outlet = await SwitchToOutlet1(await OwnerLogin());

        var res = await Refresh(outlet.Refresh);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var newAccess = json.GetProperty("accessToken").GetString()!;
        var newRefresh = json.GetProperty("refreshToken").GetString()!;
        newRefresh.Should().NotBe(outlet.Refresh);
        Claim(newAccess, "owner_id").Should().Be(_ownerId.ToString());
        Claim(newAccess, "owner_access").Should().Be("true");
        (await Get("/api/owners/me", newAccess)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The rotated token keeps working a second time (chain survives).
        (await Refresh(newRefresh)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivatedOwner_ShouldNotBeAbleToRefresh_OwnerOrOutletSessions()
    {
        var owner = await OwnerLogin();
        var outlet = await SwitchToOutlet1(owner);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var o = await db.RestaurantOwners.FindAsync(_ownerId);
            o!.Deactivate();
            await db.SaveChangesAsync();
        }

        (await Refresh(owner.Refresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Refresh(outlet.Refresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
