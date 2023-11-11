using Application.Auth.Commands.ChangePassword;
using Application.Auth.Commands.Register;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Shouldly;

namespace Infrastructure.IntegrationTests.Identity
{
    public class IdentityServiceTests : DependencyInjectionFixture
    {
        private readonly RegisterCommand _command = new()
        {
            Email = "sunlight@yopmail.com",
            Password = "123456x@X",
            FirstName = "First",
            LastName = "Last"
        };
        private readonly ChangePasswordCommand _changePasswordCommand = new()
        {
            OldPassword = "123456x@X",
            NewPassword = "123456x@X",
            ConfirmPassword = "123456x@X",
        };
        private IIdentityService _identityService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IdentityServiceTests() : base()
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
            var exception = await Should.ThrowAsync<ConflictException>(() => _identityService.CreateUserAsync(_command));

            // Assert
            user.Id.ShouldNotBeNullOrWhiteSpace();
            code.ShouldNotBeNullOrWhiteSpace();
            exception.Message.ShouldBe(@"{""email"":""Email \u0027sunlight@yopmail.com\u0027 is already taken.""}");
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
            var exception = await Should.ThrowAsync<NotFoundException>(() => _identityService.ConfirmEmailAsync(userId: "userId", code: "code"));

            // Assert
            // https://elmah.io/tools/multiline-string-converter/
            exception.Message.ShouldBe(@"{""message"":""Unable to load user with ID \u0027userId\u0027.""}");

        }

        [Fact]
        public async void ChangePassword_ThrowNotFoundException()
        {
            // Act
            var exception = await Should.ThrowAsync<NotFoundException>(() => _identityService.ChangePassword(userId: "userId", request: _changePasswordCommand));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""User is not found!""}");
        }

        [Fact]
        public async void ChangePassword_ThrowBadRequestException()
        {
            // Arrange
            var (user, code) = await _identityService.CreateUserAsync(_command);
            _changePasswordCommand.OldPassword = Guid.NewGuid().ToString();

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _identityService.ChangePassword(user.Id, request: _changePasswordCommand));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Incorrect password.""}");
        }

        [Fact]
        public async void ChangePassword_Should_Success()
        {
            // Arrange
            var (user, _) = await _identityService.CreateUserAsync(_command);
            _changePasswordCommand.OldPassword = _command.Password;

            // Act
            var exception = await Should.ThrowAsync<InvalidOperationException>(() => _identityService.ChangePassword(user.Id, _changePasswordCommand));

            // Assert
            exception.Message.ShouldBe("HttpContext must not be null.");
        }
    }
}
