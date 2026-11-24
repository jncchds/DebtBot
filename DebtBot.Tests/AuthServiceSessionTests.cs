using DebtBot.Models.Auth;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.IdentityModel.Tokens.Jwt;

namespace DebtBot.Tests;

public class AuthServiceSessionTests : IDisposable
{
    private readonly AuthServiceFixture _f = new();

    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task IssuedToken_IsRandomAndStoredOnlyAsHash()
    {
        var user = _f.AddUser();

        var a = await _f.Service.IssueRefreshTokenAsync(user.Id);
        var b = await _f.Service.IssueRefreshTokenAsync(user.Id);

        Assert.NotEqual(a.Token, b.Token);
        Assert.True(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(a.Token).Length == 32);
        var rows = await _f.Db.RefreshTokens.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.TokenHash == a.Token || r.TokenHash == b.Token);
        Assert.Contains(rows, r => r.TokenHash == AuthService.HashToken(a.Token));
        Assert.Equal(_f.Time.Now.UtcDateTime.AddDays(30), a.ExpiresAt);
    }

    [Fact]
    public async Task Refresh_RotatesToken()
    {
        var user = _f.AddUser();
        var first = await _f.Service.IssueRefreshTokenAsync(user.Id);

        var refreshed = await _f.Service.RefreshAsync(first.Token);

        Assert.NotNull(refreshed);
        Assert.Equal(user.Id, refreshed!.User.Id);
        Assert.NotEqual(first.Token, refreshed.RefreshToken.Token);

        var old = await _f.Db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == AuthService.HashToken(first.Token));
        var replacement = await _f.Db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == AuthService.HashToken(refreshed.RefreshToken.Token));
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(replacement.Id, old.ReplacedById);
        Assert.Null(replacement.RevokedAt);

        // The new token keeps working and rotates again.
        Assert.NotNull(await _f.Service.RefreshAsync(refreshed.RefreshToken.Token));
    }

    [Fact]
    public async Task ReusingRotatedToken_RevokesWholeChain()
    {
        var user = _f.AddUser();
        var first = await _f.Service.IssueRefreshTokenAsync(user.Id);
        var second = (await _f.Service.RefreshAsync(first.Token))!.RefreshToken;
        var third = (await _f.Service.RefreshAsync(second.Token))!.RefreshToken;

        // An attacker replays the first token.
        Assert.Null(await _f.Service.RefreshAsync(first.Token));

        // Everything that descended from it is now dead, including the legitimate latest token.
        Assert.Null(await _f.Service.RefreshAsync(third.Token));
        Assert.All(await _f.Db.RefreshTokens.AsNoTracking().ToListAsync(), t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task ReuseDetection_DoesNotTouchOtherSessions()
    {
        var user = _f.AddUser();
        var first = await _f.Service.IssueRefreshTokenAsync(user.Id);
        var otherBrowser = await _f.Service.IssueRefreshTokenAsync(user.Id);
        await _f.Service.RefreshAsync(first.Token);

        Assert.Null(await _f.Service.RefreshAsync(first.Token));

        Assert.NotNull(await _f.Service.RefreshAsync(otherBrowser.Token));
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var user = _f.AddUser();
        var token = await _f.Service.IssueRefreshTokenAsync(user.Id);

        _f.Time.Advance(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));

        Assert.Null(await _f.Service.RefreshAsync(token.Token));
    }

    [Fact]
    public async Task UnknownToken_IsRejected()
    {
        Assert.Null(await _f.Service.RefreshAsync("not-a-token"));
        Assert.Null(await _f.Service.RefreshAsync(""));
    }

    [Fact]
    public async Task Logout_RevokesToken()
    {
        var user = _f.AddUser();
        var token = await _f.Service.IssueRefreshTokenAsync(user.Id);

        await _f.Service.RevokeRefreshTokenAsync(token.Token);

        Assert.Null(await _f.Service.RefreshAsync(token.Token));
    }

    [Fact]
    public void CreateSession_HasUserAndAccessToken()
    {
        var user = _f.AddUser("Anna Admin", role: UserRole.Admin);
        var model = AuthServiceFixture.ToModel(user);

        var session = _f.Service.CreateSession(model);

        Assert.Equal(user.Id, session.User.Id);
        Assert.True(session.User.IsAdmin);
        Assert.Equal("Anna Admin", session.User.DisplayName);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type.EndsWith("nameidentifier")).Value);
        Assert.Contains(jwt.Claims, c => c.Type == "Admin");
        Assert.InRange(session.AccessTokenExpiresAt, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public async Task TelegramSignIn_ActualizesAndFindsUserLikeTheBot()
    {
        var authDate = _f.Time.Now.AddMinutes(-1).ToUnixTimeSeconds();
        var payload = new TelegramLoginModel { Id = 555, FirstName = "Anna", Username = "anna_iv", AuthDate = authDate };
        payload.Hash = TelegramLoginVerifier.ComputeHash(payload, AuthServiceFixture.BotToken);
        var expected = new UserModel { Id = Guid.NewGuid(), DisplayName = "Anna", TelegramId = 555 };
        _f.UserService.FindOrAddUser(Arg.Is<UserSearchModel>(m => m.TelegramId == 555), null).Returns(expected);

        var result = await _f.Service.SignInWithTelegramAsync(payload);

        Assert.Same(expected, result.User);
        _f.UserService.Received(1).ActualizeTelegramUser(555, "anna_iv", "Anna", null);
    }

    [Fact]
    public async Task TelegramSignIn_BadHash_DoesNotTouchUsers()
    {
        var payload = new TelegramLoginModel { Id = 555, FirstName = "Anna", AuthDate = _f.Time.Now.ToUnixTimeSeconds(), Hash = new string('0', 64) };

        var result = await _f.Service.SignInWithTelegramAsync(payload);

        Assert.False(result.Succeeded);
        _f.UserService.DidNotReceiveWithAnyArgs().ActualizeTelegramUser(default, default, default!, default);
        _f.UserService.DidNotReceiveWithAnyArgs().FindOrAddUser(default!, default);
    }

    [Fact]
    public async Task TelegramSignIn_WithoutBotToken_IsRejected()
    {
        _f.Config.Telegram.BotToken = "";
        var payload = new TelegramLoginModel { Id = 555, FirstName = "Anna", AuthDate = _f.Time.Now.ToUnixTimeSeconds() };
        payload.Hash = TelegramLoginVerifier.ComputeHash(payload, "");

        Assert.False((await _f.Service.SignInWithTelegramAsync(payload)).Succeeded);
    }
}
