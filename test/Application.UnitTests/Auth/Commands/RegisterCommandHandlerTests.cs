using Application.Auth.Commands.Register;
using Application.Auth.DTOs;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Application.Common.Logging;
using MediatR;
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
        private readonly Mock<IPublisher> _publisher;

        // setup
        public RegisterCommandHandlerTests()
        {
            _logTraceMock = new Mock<LogTraceBase>();
            _identityServiceMock = new Mock<IIdentityService>();
            _publisher = new Mock<IPublisher>();
        }

        // teardown
        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        [Fact]
        public async void NewUserId_Should_Success()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var user = new UserProfileDto()
            {
                Id = userId,
                Email = _registerCommand.Email,
                FirstName = _registerCommand.FirstName,
                LastName = _registerCommand.LastName,
            };
            var code = "thisIsRegisterConfirmCode";
            _identityServiceMock.Setup(x => x.CreateUserAsync(_registerCommand))
                .ReturnsAsync((user, code));

            // Act
            var handler = new RegisterCommandHandler(_publisher.Object, _logTraceMock.Object, _identityServiceMock.Object);
            var result = await handler.Handle(_registerCommand, default);

            // Assert
            result.UserId.ShouldBe(userId);
            _publisher.Verify(c => c.Publish(
                It.Is<RegisterSuccessEvent>(x => x.User == user && x.Code == code)
                , It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async void ThrowConflictException_WhenEmailIsDuplicate()
        {
            // Arrange
            _identityServiceMock.Setup(x => x.CreateUserAsync(_registerCommand))
                .Throws(new ConflictException($"{_registerCommand.Email} is duplicate!"));

            // Act
            var handler = new RegisterCommandHandler(_publisher.Object, _logTraceMock.Object, _identityServiceMock.Object);
            var exception = await Should.ThrowAsync<ConflictException>(() => handler.Handle(_registerCommand, default));

            // Assert
            exception.Message.ShouldBe("{\"message\":\"sunlight@yopmail.com is duplicate!\"}");
            _publisher.Verify(c => c.Publish(
                It.IsAny<RegisterSuccessEvent>()
                , It.IsAny<CancellationToken>()), Times.Never());
        }
    }
}
