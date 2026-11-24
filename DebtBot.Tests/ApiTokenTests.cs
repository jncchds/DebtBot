using DebtBot.DB.Entities;
using DebtBot.Identity;
using DebtBot.Models.Enums;
using DebtBot.Models.Profile;
using DebtBot.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DebtBot.Tests;

/// <summary>Series 2, T2: personal API tokens (the service, the auth scheme, scopes, JWT-only endpoints, admin, merge).</summary>
public sealed class ApiTokenTests : IAsyncDisposable
{
    private readonly ApiTokenHost _h = ApiTokenHost.Start();
    private readonly User _admin;
    private readonly User _anna;
    private readonly User _boris;

    public ApiTokenTests()
    {
        _admin = _h.F.AddUser("Admin", role: UserRole.Admin);
        _anna = _h.F.AddUser("Anna", telegramId: 1001);
        _boris = _h.F.AddUser("Boris", telegramId: 1002);
    }

    public ValueTask DisposeAsync() => _h.DisposeAsync();

    private ApiTokenService Tokens => _h.Tokens;

    private static ApiTokenCreationModel New(string name = "Script", ApiTokenScope scope = ApiTokenScope.Read, int? days = 90) =>
        new() { Name = name, Scope = scope, ExpiresInDays = days };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // --- creating, listing, revoking ---

    [Fact]
    public async Task Create_returns_the_secret_once_and_stores_only_its_hash()
    {
        var result = await Tokens.CreateAsync(_anna.Id, New("Home Assistant", ApiTokenScope.ReadWrite, 30));

        Assert.Equal(ApiTokenCreateStatus.Created, result.Status);
        var created = result.Token!;
        Assert.StartsWith("dbt_", created.Token);
        Assert.Equal(4 + 43, created.Token.Length); // 32 random bytes, base64url without padding
        Assert.Equal(created.Token.Substring(4, 8), created.Prefix);
        Assert.Equal(ApiTokenStatus.Active, created.Status);
        Assert.Equal(_h.Time.Now.UtcDateTime.AddDays(30), created.ExpiresAt);

        _h.F.Clear();
        var row = _h.F.Db.ApiTokens.Single();
        Assert.Equal(AuthService.HashToken(created.Token), row.Hash);
        Assert.DoesNotContain(created.Token, row.Hash);
        Assert.Equal("Home Assistant", row.Name);

        var other = await Tokens.CreateAsync(_anna.Id, New(days: null));
        Assert.NotEqual(created.Token, other.Token!.Token);
        Assert.Null(other.Token.ExpiresAt);
    }

    [Fact]
    public async Task Create_over_http_returns_the_token_and_list_never_does()
    {
        var jwt = _h.Jwt(_anna);
        var post = await _h.Send(HttpMethod.Post, "/api/v1/profile/tokens", jwt, new { name = "CI", scope = "ReadWrite", expiresInDays = (int?)null });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var created = await Json(post);
        var secret = created.GetProperty("token").GetString()!;
        Assert.StartsWith("dbt_", secret);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("expiresAt").ValueKind);
        Assert.Equal("ReadWrite", created.GetProperty("scope").GetString());

        var list = await _h.Send(HttpMethod.Get, "/api/v1/profile/tokens", jwt);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var text = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, text);
        Assert.DoesNotContain("\"token\"", text);
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
        var item = (await Json(list))[0];
        Assert.Equal("CI", item.GetProperty("name").GetString());
        Assert.Equal(secret.Substring(4, 8), item.GetProperty("prefix").GetString());
        Assert.Equal("Active", item.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("", 90)]
    [InlineData("   ", 90)]
    [InlineData(null, 7)]
    [InlineData(null, 0)]
    public async Task Create_refuses_bad_names_and_expiries(string? name, int days)
    {
        var result = await Tokens.CreateAsync(_anna.Id, New(name ?? "ok", days: days));
        Assert.Equal(ApiTokenCreateStatus.Invalid, result.Status);

        var tooLong = await Tokens.CreateAsync(_anna.Id, New(new string('x', 101)));
        Assert.Equal(ApiTokenCreateStatus.Invalid, tooLong.Status);
        Assert.Equal(ApiTokenCreateStatus.Created, (await Tokens.CreateAsync(_anna.Id, New(new string('x', 100)))).Status);

        var http = await _h.Send(HttpMethod.Post, "/api/v1/profile/tokens", _h.Jwt(_anna), new { name = "x", scope = "Read", expiresInDays = 45 });
        Assert.Equal(HttpStatusCode.BadRequest, http.StatusCode);
    }

    [Fact]
    public async Task At_most_20_active_tokens_and_expired_or_revoked_ones_dont_count()
    {
        var first = await Tokens.CreateAsync(_anna.Id, New("first"));
        for (var i = 0; i < 18; i++)
        {
            Assert.Equal(ApiTokenCreateStatus.Created, (await Tokens.CreateAsync(_anna.Id, New($"t{i}"))).Status);
        }
        Assert.Equal(ApiTokenCreateStatus.Created, (await Tokens.CreateAsync(_anna.Id, New("short", days: 30))).Status);

        Assert.Equal(ApiTokenCreateStatus.LimitReached, (await Tokens.CreateAsync(_anna.Id, New("one too many"))).Status);
        var http = await _h.Send(HttpMethod.Post, "/api/v1/profile/tokens", _h.Jwt(_anna), new { name = "x", scope = "Read", expiresInDays = 90 });
        Assert.Equal(HttpStatusCode.Conflict, http.StatusCode);

        // Boris isn't affected.
        Assert.Equal(ApiTokenCreateStatus.Created, (await Tokens.CreateAsync(_boris.Id, New())).Status);

        // Revoking one frees a place.
        Assert.True(await Tokens.RevokeAsync(_anna.Id, first.Token!.Id));
        Assert.Equal(ApiTokenCreateStatus.Created, (await Tokens.CreateAsync(_anna.Id, New("after revoke"))).Status);
        Assert.Equal(ApiTokenCreateStatus.LimitReached, (await Tokens.CreateAsync(_anna.Id, New())).Status);

        // So does expiry: after 31 days the 30-day token no longer counts.
        _h.Time.Advance(TimeSpan.FromDays(31));
        Assert.Equal(ApiTokenCreateStatus.Created, (await Tokens.CreateAsync(_anna.Id, New("after expiry"))).Status);
        Assert.Equal(ApiTokenCreateStatus.LimitReached, (await Tokens.CreateAsync(_anna.Id, New())).Status);
    }

    [Fact]
    public async Task Revoke_is_idempotent_and_someone_elses_token_is_404()
    {
        var token = (await Tokens.CreateAsync(_anna.Id, New())).Token!;
        var jwt = _h.Jwt(_anna);

        var boris = await _h.Send(HttpMethod.Delete, $"/api/v1/profile/tokens/{token.Id}", _h.Jwt(_boris));
        Assert.Equal(HttpStatusCode.NotFound, boris.StatusCode);
        var adminTry = await _h.Send(HttpMethod.Delete, $"/api/v1/profile/tokens/{token.Id}", _h.Jwt(_admin));
        Assert.Equal(HttpStatusCode.NotFound, adminTry.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _h.Send(HttpMethod.Delete, $"/api/v1/profile/tokens/{Guid.NewGuid()}", jwt)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _h.Send(HttpMethod.Delete, $"/api/v1/profile/tokens/{token.Id}", jwt)).StatusCode);
        _h.F.Clear();
        var revokedAt = _h.F.Db.ApiTokens.Single().RevokedAt;
        Assert.NotNull(revokedAt);

        _h.Time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(HttpStatusCode.NoContent, (await _h.Send(HttpMethod.Delete, $"/api/v1/profile/tokens/{token.Id}", jwt)).StatusCode);
        _h.F.Clear();
        Assert.Equal(revokedAt, _h.F.Db.ApiTokens.Single().RevokedAt);
    }

    [Fact]
    public async Task List_puts_active_first_then_expired_then_revoked()
    {
        var revoked = (await Tokens.CreateAsync(_anna.Id, New("revoked"))).Token!;
        await Tokens.RevokeAsync(_anna.Id, revoked.Id);
        _h.Time.Advance(TimeSpan.FromSeconds(1));
        await Tokens.CreateAsync(_anna.Id, New("expires", days: 30));
        _h.Time.Advance(TimeSpan.FromSeconds(1));
        await Tokens.CreateAsync(_anna.Id, New("old", days: null));
        _h.Time.Advance(TimeSpan.FromDays(31));
        await Tokens.CreateAsync(_anna.Id, New("new", days: null));
        await Tokens.CreateAsync(_boris.Id, New("boris"));

        var list = Tokens.List(_anna.Id);
        Assert.Equal(["new", "old", "expires", "revoked"], list.Select(t => t.Name));
        Assert.Equal([ApiTokenStatus.Active, ApiTokenStatus.Active, ApiTokenStatus.Expired, ApiTokenStatus.Revoked], list.Select(t => t.Status));
    }

    // --- authentication ---

    [Fact]
    public async Task Handler_accepts_a_valid_token_and_refuses_revoked_expired_unknown_and_deleted_users()
    {
        var valid = _h.Pat(_anna, ApiTokenScope.Read);
        var me = await _h.Send(HttpMethod.Get, "/api/v1/profile", valid);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(_anna.Id.ToString(), (await Json(me)).GetProperty("id").GetString());

        var identity = await Tokens.AuthenticateAsync(valid);
        Assert.NotNull(identity);
        Assert.Equal(_anna.Id, identity!.UserId);
        Assert.Equal(ApiTokenScope.Read, identity.Scope);

        // Unknown, malformed and not-a-token values
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", "dbt_" + new string('A', 43))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", "dbt_")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", "not-a-jwt")).StatusCode);
        Assert.Null(await Tokens.AuthenticateAsync("xyz"));

        // Revoked
        var revoked = _h.Pat(_anna);
        var revokedId = _h.F.Db.ApiTokens.AsNoTracking().Single(t => t.Hash == AuthService.HashToken(revoked)).Id;
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", revoked)).StatusCode);
        await Tokens.RevokeAsync(_anna.Id, revokedId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", revoked)).StatusCode);

        // Expired
        var expiring = _h.Pat(_anna, expiresInDays: 30);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", expiring)).StatusCode);
        _h.Time.Advance(TimeSpan.FromDays(30));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", expiring)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", valid)).StatusCode);

        // Deleted user: the tokens go with them
        var borisToken = _h.Pat(_boris);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", borisToken)).StatusCode);
        _h.F.Db.Users.Remove(_h.F.Db.Users.Single(u => u.Id == _boris.Id));
        _h.F.Db.SaveChanges();
        _h.F.Clear();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", borisToken)).StatusCode);
        Assert.Null(await Tokens.AuthenticateAsync(borisToken));
    }

    [Fact]
    public async Task A_jwt_still_works_and_the_scheme_is_picked_by_the_prefix()
    {
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", _h.Jwt(_anna))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", null)).StatusCode);

        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer dbt_abc";
        Assert.Equal(IdentityData.ApiTokenScheme, AuthSetup.SelectScheme(context));
        context.Request.Headers.Authorization = "bearer dbt_abc";
        Assert.Equal(IdentityData.ApiTokenScheme, AuthSetup.SelectScheme(context));
        context.Request.Headers.Authorization = "Bearer eyJhbGciOi.x.y";
        Assert.Equal(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, AuthSetup.SelectScheme(context));
        context.Request.Headers.Remove("Authorization");
        Assert.Equal(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, AuthSetup.SelectScheme(context));
    }

    [Fact]
    public async Task An_admin_token_gets_admin_and_a_role_change_applies_at_once()
    {
        var adminPat = _h.Pat(_admin, ApiTokenScope.Read);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/Users", adminPat)).StatusCode);

        var annaPat = _h.Pat(_anna, ApiTokenScope.Read);
        Assert.Equal(HttpStatusCode.Forbidden, (await _h.Send(HttpMethod.Get, "/api/v1/Users", annaPat)).StatusCode);

        Assert.True(_h.F.UserService.SetRole(_anna.Id, UserRole.Admin));
        _h.F.Clear();
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/Users", annaPat)).StatusCode);

        Assert.True(_h.F.UserService.SetRole(_anna.Id, UserRole.Regular));
        _h.F.Clear();
        Assert.Equal(HttpStatusCode.Forbidden, (await _h.Send(HttpMethod.Get, "/api/v1/Users", annaPat)).StatusCode);

        // The display name comes from the database too.
        var user = _h.F.Db.Users.Single(u => u.Id == _anna.Id);
        user.DisplayName = "Anna P.";
        _h.F.Db.SaveChanges();
        _h.F.Clear();
        var identity = await Tokens.AuthenticateAsync(annaPat);
        Assert.Equal("Anna P.", identity!.DisplayName);
    }

    [Fact]
    public async Task LastUsedAt_is_written_at_most_once_a_minute()
    {
        var pat = _h.Pat(_anna);
        DateTime? LastUsed()
        {
            _h.F.Clear();
            return _h.F.Db.ApiTokens.AsNoTracking().Single().LastUsedAt;
        }
        Assert.Null(LastUsed());

        var t0 = _h.Time.Now.UtcDateTime;
        await Tokens.AuthenticateAsync(pat);
        Assert.Equal(t0, LastUsed());

        _h.Time.Advance(TimeSpan.FromSeconds(30));
        await Tokens.AuthenticateAsync(pat);
        await Tokens.AuthenticateAsync(pat);
        Assert.Equal(t0, LastUsed());

        _h.Time.Advance(TimeSpan.FromSeconds(31));
        var t1 = _h.Time.Now.UtcDateTime;
        await _h.Send(HttpMethod.Get, "/api/v1/profile", pat);
        Assert.Equal(t1, LastUsed());

        _h.Time.Advance(TimeSpan.FromSeconds(10));
        await _h.Send(HttpMethod.Get, "/api/v1/profile", pat);
        Assert.Equal(t1, LastUsed());
    }

    // --- scopes ---

    [Fact]
    public async Task A_read_only_token_gets_403_on_writes_and_GET_works()
    {
        var read = _h.Pat(_anna, ApiTokenScope.Read);
        var bill = new
        {
            currencyCode = "EUR",
            description = "Pizza",
            lines = new[] { new { itemDescription = "Pizza", subtotal = 20m, participants = new[] { new { userId = _anna.Id }, new { userId = _boris.Id } } } },
            payments = new[] { new { userId = _anna.Id, amount = 20m } },
        };

        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", read)).StatusCode);
        Assert.True(ApiTokenScopeMiddleware.IsReadMethod("GET"));
        Assert.True(ApiTokenScopeMiddleware.IsReadMethod("HEAD"));
        Assert.True(ApiTokenScopeMiddleware.IsReadMethod("OPTIONS"));
        Assert.False(ApiTokenScopeMiddleware.IsReadMethod("PATCH"));

        var post = await _h.Send(HttpMethod.Post, "/api/v1/Bills", read, bill);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(ApiTokenScopeMiddleware.Message, (await Json(post)).GetProperty("message").GetString());

        // A draft made with a session, then PUT and DELETE with the read-only token
        var created = await _h.Send(HttpMethod.Post, "/api/v1/Bills", _h.Jwt(_anna), bill);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var billId = (await Json(created)).GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await _h.Send(HttpMethod.Put, $"/api/v1/Bills/{billId}", read, bill)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _h.Send(HttpMethod.Delete, $"/api/v1/Users/{_boris.Id}", _h.Pat(_admin, ApiTokenScope.Read))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _h.Send(HttpMethod.Put, "/api/v1/profile", read, new { displayName = "Hacked" })).StatusCode);
        _h.F.Clear();
        Assert.Equal("Anna", _h.F.Db.Users.Single(u => u.Id == _anna.Id).DisplayName);

        // A read-and-write token can write
        var write = _h.Pat(_anna, ApiTokenScope.ReadWrite);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Post, "/api/v1/Bills", write, bill)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Put, $"/api/v1/Bills/{billId}", write, bill)).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/profile/tokens")]
    [InlineData("POST", "/api/v1/profile/tokens")]
    [InlineData("DELETE", "/api/v1/profile/tokens/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/v1/auth/password")]
    [InlineData("POST", "/api/v1/auth/refresh")]
    [InlineData("POST", "/api/v1/auth/logout")]
    [InlineData("POST", "/api/v1/Users/{boris}/password")]
    [InlineData("POST", "/api/v1/Users/{boris}/tokens/revoke-all")]
    public async Task A_token_gets_403_on_the_jwt_only_endpoints(string method, string url)
    {
        url = url.Replace("{boris}", _boris.Id.ToString());
        // An admin read-and-write token: neither scope nor role is the reason for the 403.
        var pat = _h.Pat(_admin, ApiTokenScope.ReadWrite);
        var body = new { name = "x", scope = "Read", expiresInDays = 30, login = "boris", password = "password123", newPassword = "password123" };

        var response = await _h.Send(new HttpMethod(method), url, pat, body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(JwtOnlyAttribute.Message, (await Json(response)).GetProperty("message").GetString());

        // The same call with a session isn't refused for being a session (refresh still wants its cookie: 401).
        var jwt = await _h.Send(new HttpMethod(method), url, _h.Jwt(_admin), body);
        Assert.NotEqual(HttpStatusCode.Forbidden, jwt.StatusCode);
        Assert.DoesNotContain(JwtOnlyAttribute.Message, await jwt.Content.ReadAsStringAsync());
    }

    [Fact]
    public void The_jwt_only_endpoints_carry_the_attribute()
    {
        static bool JwtOnly(Type controller, string method) =>
            controller.GetMethod(method)!.GetCustomAttributes(typeof(JwtOnlyAttribute), true).Length == 1;

        Assert.True(JwtOnly(typeof(Controllers.ProfileController), nameof(Controllers.ProfileController.GetTokens)));
        Assert.True(JwtOnly(typeof(Controllers.ProfileController), nameof(Controllers.ProfileController.CreateToken)));
        Assert.True(JwtOnly(typeof(Controllers.ProfileController), nameof(Controllers.ProfileController.RevokeToken)));
        Assert.True(JwtOnly(typeof(Controllers.AuthController), nameof(Controllers.AuthController.SetPassword)));
        Assert.True(JwtOnly(typeof(Controllers.AuthController), nameof(Controllers.AuthController.Refresh)));
        Assert.True(JwtOnly(typeof(Controllers.AuthController), nameof(Controllers.AuthController.Logout)));
        Assert.True(JwtOnly(typeof(Controllers.UsersController), nameof(Controllers.UsersController.SetPassword)));
        Assert.True(JwtOnly(typeof(Controllers.UsersController), nameof(Controllers.UsersController.RevokeAllTokens)));
    }

    // --- admin ---

    [Fact]
    public async Task Revoke_all_is_admin_only_and_the_count_is_in_the_users_list()
    {
        var a1 = _h.Pat(_anna);
        var a2 = _h.Pat(_anna, ApiTokenScope.Read);
        var expired = _h.Pat(_anna, expiresInDays: 30);
        var expiredRow = _h.F.Db.ApiTokens.Single(t => t.Hash == AuthService.HashToken(expired));
        expiredRow.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        _h.F.Db.SaveChanges();
        var revoked = (await Tokens.CreateAsync(_anna.Id, New())).Token!;
        await Tokens.RevokeAsync(_anna.Id, revoked.Id);
        var borisPat = _h.Pat(_boris);
        _h.F.Clear();

        // The admin list counts active tokens only.
        var users = _h.F.UserService.GetAdminUsers(null, null, 0, 50).Items;
        Assert.Equal(2, users.Single(u => u.Id == _anna.Id).ApiTokenCount);
        Assert.Equal(1, users.Single(u => u.Id == _boris.Id).ApiTokenCount);
        Assert.Equal(0, users.Single(u => u.Id == _admin.Id).ApiTokenCount);

        var asAnna = await _h.Send(HttpMethod.Post, $"/api/v1/Users/{_boris.Id}/tokens/revoke-all", _h.Jwt(_anna));
        Assert.Equal(HttpStatusCode.Forbidden, asAnna.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _h.Send(HttpMethod.Post, $"/api/v1/Users/{Guid.NewGuid()}/tokens/revoke-all", _h.Jwt(_admin))).StatusCode);

        var response = await _h.Send(HttpMethod.Post, $"/api/v1/Users/{_anna.Id}/tokens/revoke-all", _h.Jwt(_admin));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await Json(response)).GetProperty("revoked").GetInt32());

        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", a1)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", a2)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", expired)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", borisPat)).StatusCode);

        // The admin list never carries names or prefixes.
        var list = await _h.Send(HttpMethod.Get, "/api/v1/Users", _h.Jwt(_admin));
        var text = await list.Content.ReadAsStringAsync();
        Assert.Contains("\"apiTokenCount\":1", text);
        Assert.DoesNotContain("prefix", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(borisPat.Substring(4, 8), text);
    }

    [Fact]
    public async Task Merging_users_revokes_the_merged_users_tokens_and_keeps_the_kept_users()
    {
        var dup = _h.F.AddUser("@anna_old", telegramUserName: "@anna_old");
        var oldPat = _h.Pat(dup);
        var keptPat = _h.Pat(_anna);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", oldPat)).StatusCode);

        var merge = await _h.Send(HttpMethod.Post, "/api/v1/Users/merge", _h.Jwt(_admin), new { oldUserId = dup.Id, newUserId = _anna.Id });
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);
        _h.F.Clear();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.Send(HttpMethod.Get, "/api/v1/profile", oldPat)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", keptPat)).StatusCode);
        Assert.Single(_h.F.Db.ApiTokens.AsNoTracking().ToList());
        Assert.Single(Tokens.List(_anna.Id));
    }
}

/// <summary>The per-token rate limit, in a host with a low limit.</summary>
public sealed class ApiTokenRateLimitTests : IAsyncDisposable
{
    private readonly ApiTokenHost _h = ApiTokenHost.Start(rateLimitPerMinute: 5);

    public ValueTask DisposeAsync() => _h.DisposeAsync();

    [Fact]
    public async Task Each_token_has_its_own_window_and_sessions_arent_limited()
    {
        var anna = _h.F.AddUser("Anna");
        var first = _h.Pat(anna);
        var second = _h.Pat(anna);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", first)).StatusCode);
        }
        var limited = await _h.Send(HttpMethod.Get, "/api/v1/profile", first);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(AuthSetup.RateLimitMessage, JsonDocument.Parse(await limited.Content.ReadAsStringAsync()).RootElement.GetProperty("message").GetString());

        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", second)).StatusCode);

        var jwt = _h.Jwt(anna);
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, "/api/v1/profile", jwt)).StatusCode);
        }
    }
}
