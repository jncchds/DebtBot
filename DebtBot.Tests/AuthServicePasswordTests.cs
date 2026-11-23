using DebtBot.Models.Auth;
using DebtBot.Services;
using Microsoft.AspNetCore.Identity;
using DbUser = DebtBot.DB.Entities.User;

namespace DebtBot.Tests;

public class AuthServicePasswordTests : IDisposable
{
    private readonly AuthServiceFixture _f = new();

    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task FirstTime_SetsLoginLowercaseAndPassword_ThenSignInWorks()
    {
        var user = _f.AddUser();

        var result = await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "  Anna.Iv ", NewPassword = "correct horse" });

        Assert.Equal(PasswordChangeResult.Success, result);
        var stored = _f.Reload(user.Id);
        Assert.Equal("anna.iv", stored.Login);
        Assert.NotNull(stored.PasswordHash);
        Assert.DoesNotContain("correct horse", stored.PasswordHash);

        var signIn = await _f.Service.SignInWithPasswordAsync("ANNA.IV", "correct horse");
        Assert.True(signIn.Succeeded);
        Assert.Equal(user.Id, signIn.User!.Id);
    }

    [Fact]
    public async Task FirstTime_RequiresLoginAndPassword()
    {
        var user = _f.AddUser();

        Assert.Equal(PasswordChangeResult.LoginRequired,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { NewPassword = "correct horse" }));
        Assert.Equal(PasswordChangeResult.PasswordRequired,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna" }));
        Assert.Null(_f.Reload(user.Id).PasswordHash);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("кириллица")]
    [InlineData("semi;colon")]
    public async Task InvalidLogin_IsRejected(string login)
    {
        var user = _f.AddUser();

        Assert.Equal(PasswordChangeResult.InvalidLogin,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = login, NewPassword = "correct horse" }));
    }

    [Fact]
    public async Task ShortPassword_IsRejected()
    {
        var user = _f.AddUser();

        Assert.Equal(PasswordChangeResult.PasswordTooShort,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna", NewPassword = "1234567" }));
        Assert.Null(_f.Reload(user.Id).Login);
    }

    [Fact]
    public async Task TakenLogin_IsRejected_IgnoringCase()
    {
        _f.AddUser("Boris", login: "boris");
        var user = _f.AddUser();

        Assert.Equal(PasswordChangeResult.LoginTaken,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "Boris", NewPassword = "correct horse" }));
    }

    [Fact]
    public async Task Change_NeedsCorrectCurrentPassword()
    {
        var user = _f.AddUser();
        await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna", NewPassword = "correct horse" });

        Assert.Equal(PasswordChangeResult.CurrentPasswordRequired,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { NewPassword = "battery staple" }));
        Assert.Equal(PasswordChangeResult.CurrentPasswordIncorrect,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { CurrentPassword = "wrong one", NewPassword = "battery staple" }));
        Assert.Equal(PasswordChangeResult.CurrentPasswordRequired,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna2" }));

        Assert.Equal(PasswordChangeResult.Success,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { CurrentPassword = "correct horse", NewPassword = "battery staple" }));

        Assert.False((await _f.Service.SignInWithPasswordAsync("anna", "correct horse")).Succeeded);
        Assert.True((await _f.Service.SignInWithPasswordAsync("anna", "battery staple")).Succeeded);
    }

    [Fact]
    public async Task Change_LoginOnly_KeepsPassword()
    {
        var user = _f.AddUser();
        await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna", NewPassword = "correct horse" });

        Assert.Equal(PasswordChangeResult.Success,
            await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna2", CurrentPassword = "correct horse" }));

        Assert.False((await _f.Service.SignInWithPasswordAsync("anna", "correct horse")).Succeeded);
        Assert.True((await _f.Service.SignInWithPasswordAsync("anna2", "correct horse")).Succeeded);
    }

    [Fact]
    public async Task SignIn_WrongLoginAndWrongPassword_LookTheSame()
    {
        var user = _f.AddUser();
        await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna", NewPassword = "correct horse" });

        var wrongPassword = await _f.Service.SignInWithPasswordAsync("anna", "nope nope");
        var wrongLogin = await _f.Service.SignInWithPasswordAsync("nobody", "correct horse");

        Assert.False(wrongPassword.Succeeded);
        Assert.False(wrongLogin.Succeeded);
        Assert.Equal(wrongPassword.Error, wrongLogin.Error);
        Assert.Equal(AuthService.InvalidCredentialsMessage, wrongLogin.Error);
    }

    [Fact]
    public async Task SignIn_UserWithLoginButNoPassword_Fails()
    {
        _f.AddUser(login: "anna");

        Assert.False((await _f.Service.SignInWithPasswordAsync("anna", "anything at all")).Succeeded);
    }

    [Fact]
    public async Task SignIn_LocksLoginAfterTooManyFailures()
    {
        var user = _f.AddUser();
        await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna", NewPassword = "correct horse" });

        for (var i = 0; i < LoginThrottle.MaxFailures; i++)
        {
            var failed = await _f.Service.SignInWithPasswordAsync("anna", "wrong password");
            Assert.False(failed.LockedOut);
        }

        var locked = await _f.Service.SignInWithPasswordAsync("anna", "correct horse");
        Assert.False(locked.Succeeded);
        Assert.True(locked.LockedOut);
    }

    [Fact]
    public async Task SignIn_RehashesOldFormatHash()
    {
        var user = _f.AddUser(login: "anna");
        var v2Hasher = new PasswordHasher<DbUser>(Microsoft.Extensions.Options.Options.Create(
            new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 }));
        var entity = _f.Db.Users.Single(u => u.Id == user.Id);
        entity.PasswordHash = v2Hasher.HashPassword(entity, "correct horse");
        var oldHash = entity.PasswordHash;
        _f.Db.SaveChanges();

        Assert.True((await _f.Service.SignInWithPasswordAsync("anna", "correct horse")).Succeeded);

        var rehashed = _f.Reload(user.Id).PasswordHash;
        Assert.NotEqual(oldHash, rehashed);
        Assert.True((await _f.Service.SignInWithPasswordAsync("anna", "correct horse")).Succeeded);
    }

    [Fact]
    public async Task AdminSet_SetsLoginAndPassword_WithoutCurrentPassword()
    {
        var user = _f.AddUser();
        await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna", NewPassword = "correct horse" });

        Assert.Equal(PasswordChangeResult.Success,
            await _f.Service.AdminSetPasswordAsync(user.Id, new AdminSetPasswordModel { Password = "set by admin" }));

        Assert.Equal("anna", _f.Reload(user.Id).Login);
        Assert.True((await _f.Service.SignInWithPasswordAsync("anna", "set by admin")).Succeeded);
    }

    [Fact]
    public async Task AdminSet_RequiresLoginForUserWithout_AndChecksRules()
    {
        var user = _f.AddUser();
        _f.AddUser("Boris", login: "boris");

        Assert.Equal(PasswordChangeResult.LoginRequired,
            await _f.Service.AdminSetPasswordAsync(user.Id, new AdminSetPasswordModel { Password = "set by admin" }));
        Assert.Equal(PasswordChangeResult.LoginTaken,
            await _f.Service.AdminSetPasswordAsync(user.Id, new AdminSetPasswordModel { Login = "BORIS", Password = "set by admin" }));
        Assert.Equal(PasswordChangeResult.PasswordTooShort,
            await _f.Service.AdminSetPasswordAsync(user.Id, new AdminSetPasswordModel { Login = "anna", Password = "short" }));
        Assert.Equal(PasswordChangeResult.UserNotFound,
            await _f.Service.AdminSetPasswordAsync(Guid.NewGuid(), new AdminSetPasswordModel { Login = "anna", Password = "set by admin" }));
    }

    [Fact]
    public async Task AdminSet_SignsUserOutEverywhere()
    {
        var user = _f.AddUser(login: "anna");
        var token = await _f.Service.IssueRefreshTokenAsync(user.Id);

        await _f.Service.AdminSetPasswordAsync(user.Id, new AdminSetPasswordModel { Password = "set by admin" });

        Assert.Null(await _f.Service.RefreshAsync(token.Token));
    }

    [Fact]
    public async Task OwnPasswordChange_KeepsCurrentSession_RevokesOthers()
    {
        var user = _f.AddUser();
        var current = await _f.Service.IssueRefreshTokenAsync(user.Id);
        var other = await _f.Service.IssueRefreshTokenAsync(user.Id);

        await _f.Service.SetOwnPasswordAsync(user.Id, new SetPasswordModel { Login = "anna", NewPassword = "correct horse" }, current.Token);

        Assert.NotNull(await _f.Service.RefreshAsync(current.Token));
        Assert.Null(await _f.Service.RefreshAsync(other.Token));
    }
}
