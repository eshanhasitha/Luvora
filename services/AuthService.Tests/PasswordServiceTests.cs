using AuthService.Services;

namespace AuthService.Tests;

public class PasswordServiceTests
{
    [Fact]
    public void HashPassword_ShouldNotReturnOriginalPassword()
    {
        var service = new PasswordService();

        var password = "Password123!";

        var hash = service.HashPassword(password);

        Assert.NotEqual(password, hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrue_ForCorrectPassword()
    {
        var service = new PasswordService();

        var password = "Password123!";
        var hash = service.HashPassword(password);

        var result = service.VerifyPassword(password, hash);

        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_ForIncorrectPassword()
    {
        var service = new PasswordService();

        var password = "Password123!";
        var hash = service.HashPassword(password);

        var result = service.VerifyPassword(
            "WrongPassword!",
            hash
        );

        Assert.False(result);
    }
}