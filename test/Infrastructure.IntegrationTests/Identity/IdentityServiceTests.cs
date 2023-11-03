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
        private IIdentityService _identityService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IdentityServiceTests() : base()
        {
            _identityService = GetService<IIdentityService>();
            _userManager = GetService<UserManager<ApplicationUser>>();
        }

        [Fact]
        public async void CreateUserAsync_Should_Success()
        {
            // Act
            var (user, code) = await _identityService.CreateUserAsync(_command);

            // Assert
            user.Id.ShouldNotBeNullOrWhiteSpace();
            code.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async void CreateUserAsync_ThrowConflictException()
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
        public async void CreateUserAsync_ThrowUnHandleException()
        {
            // Arrange
            _command.Email = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<Exception>(() => _identityService.CreateUserAsync(_command));

            // Assert
            exception.Message.ShouldBe("UnhandledException: IdentityService");
        }

        [Fact]
        public async void ConfirmEmailAsync_Should_Success()
        {
            // Arrange
            var (user, code) = await _identityService.CreateUserAsync(_command);

            // Act
            var success = await _identityService.ConfirmEmailAsync(user.Id, code);

            // Assert
            success.ShouldBe(true);

        }

        [Fact]
        public async void ConfirmEmailAsync_ThrowNotFoundException()
        {
            // Act
            var exception = await Should.ThrowAsync<NotFoundException>(() => _identityService.ConfirmEmailAsync(userId: "userId", code: "code"));

            // Assert
            // https://elmah.io/tools/multiline-string-converter/
            exception.Message.ShouldBe(@"{""message"":""Unable to load user with ID \u0027userId\u0027.""}");

        }
    }
}
