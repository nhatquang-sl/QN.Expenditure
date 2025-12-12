using Auth.Application.Account.Commands.ChangePassword;
using Auth.Application.Account.Commands.Register;
using Auth.Application.Common.Abstractions;
using Auth.Infrastructure.Identity;
using Lib.Application.Exceptions;
using Microsoft.AspNetCore.Identity;
using Shouldly;

namespace Auth.Infrastructure.IntegrationTests.Identity
{
    public class IdentityServiceTests : DependencyInjectionFixture
    {
        private const string ValidPassword = "123456x@X";

        private readonly RegisterCommand _command = new()
        {
            Email = "sunlight@yopmail.com",
            Password = ValidPassword,
            FirstName = "First",
            LastName = "Last"
        };

        private readonly IIdentityService _identityService;

        private readonly UserManager<ApplicationUser> _userManager;

        public IdentityServiceTests()
        {
            _identityService = GetService<IIdentityService>();
            _userManager = GetService<UserManager<ApplicationUser>>();
        }

        [Fact]
        public async void CreateUser_Should_Success()
        {
            // Act
            var (user, code) = await _identityService.CreateUserAsync(_command);

            // Assert
            user.Id.ShouldNotBeNullOrWhiteSpace();
            code.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async void CreateUser_ThrowConflictException()
        {
            // Act
            var (user, code) = await _identityService.CreateUserAsync(_command);
            var exception =
                await Should.ThrowAsync<ConflictException>(() => _identityService.CreateUserAsync(_command));

            // Assert
            user.Id.ShouldNotBeNullOrWhiteSpace();
            code.ShouldNotBeNullOrWhiteSpace();
            exception.Message.ShouldBe("""{"message":"Email \u0027sunlight@yopmail.com\u0027 is already taken."}""");
        }

        [Fact]
        public async void CreateUser_ThrowUnHandleException()
        {
            // Arrange
            _command.Email = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<Exception>(() => _identityService.CreateUserAsync(_command));

            // Assert
            exception.Message.ShouldBe("UnhandledException: IdentityService");
        }

        [Fact]
        public async void ConfirmEmail_Should_Success()
        {
            // Arrange
            var (user, code) = await _identityService.CreateUserAsync(_command);

            // Act
            var success = await _identityService.ConfirmEmailAsync(user.Id, code);

            // Assert
            success.ShouldBe(true);
        }

        [Fact]
        public async void ConfirmEmail_ThrowNotFoundException()
        {
            // Act
            var exception =
                await Should.ThrowAsync<NotFoundException>(() => _identityService.ConfirmEmailAsync("userId", "code"));

            // Assert
            // https://elmah.io/tools/multiline-string-converter/
            exception.Message.ShouldBe("""{"message":"Unable to load user with ID \u0027userId\u0027."}""");
        }

        [Fact]
        public async void ChangePassword_ThrowNotFoundException()
        {
            // Act
            var changePasswordCommand = new ChangePasswordCommand(ValidPassword, ValidPassword, ValidPassword);
            var exception = await Should.ThrowAsync<NotFoundException>(() =>
                _identityService.ChangePassword("userId", changePasswordCommand));

            // Assert
            exception.Message.ShouldBe("""{"message":"User is not found!"}""");
        }

        [Fact]
        public async void ChangePassword_ThrowBadRequestException()
        {
            // Arrange
            var changePasswordCommand = new ChangePasswordCommand("ValidPassword", ValidPassword, ValidPassword);
            var (user, code) = await _identityService.CreateUserAsync(_command);

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() =>
                _identityService.ChangePassword(user.Id, changePasswordCommand));

            // Assert
            exception.Message.ShouldBe("""{"message":"Incorrect password."}""");
        }

        [Fact]
        public async void ChangePassword_Should_Success()
        {
            // Arrange
            var changePasswordCommand = new ChangePasswordCommand(ValidPassword, ValidPassword, ValidPassword);
            var (user, _) = await _identityService.CreateUserAsync(_command);

            // Act
            var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
                _identityService.ChangePassword(user.Id, changePasswordCommand));

            // Assert
            exception.Message.ShouldBe("HttpContext must not be null.");
        }
    }
}