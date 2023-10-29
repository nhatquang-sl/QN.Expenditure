using Application.Auth.Commands.Register;
using Application.Auth.DTOs;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Application.Common.Logging;
using Moq;
using Shouldly;

namespace Application.UnitTests.Auth.Commands
{
    public class RegisterCommandHandlerTests : IDisposable
    {
        private readonly RegisterCommand _registerCommand = new()
        {
            Email = "sunlight@yopmail.com",
            Password = "P@ssw0rd",
            FirstName = "First",
            LastName = "Last"
        };
        private readonly Mock<LogTraceBase> _logTraceMock;
        private readonly Mock<IIdentityService> _identityServiceMock;

        // setup
        public RegisterCommandHandlerTests()
        {
            _logTraceMock = new Mock<LogTraceBase>();
            _identityServiceMock = new Mock<IIdentityService>();
        }

        // teardown
        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        [Fact]
        public async void NewUserId_WhenCreatingSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            _identityServiceMock.Setup(x => x.CreateUserAsync(_registerCommand))
                .ReturnsAsync((new UserProfileDto()
                {
                    Id = userId,
                }, ""));

            // Act
            var handler = new RegisterCommandHandler(_logTraceMock.Object, _identityServiceMock.Object);
            var result = await handler.Handle(_registerCommand, default);

            // Assert
            result.UserId.ShouldBe(userId);
        }

        [Fact]
        public async void ThrowConflictException_WhenEmailIsDuplicate()
        {
            // Arrange
            _identityServiceMock.Setup(x => x.CreateUserAsync(_registerCommand))
                .Throws(new ConflictException($"{_registerCommand.Email} is duplicate!"));

            // Act
            var handler = new RegisterCommandHandler(_logTraceMock.Object, _identityServiceMock.Object);
            var exception = await Should.ThrowAsync<ConflictException>(() => handler.Handle(_registerCommand, default));

            // Assert
            exception.Message.ShouldBe("{\"message\":\"sunlight@yopmail.com is duplicate!\"}");
        }
    }
}
