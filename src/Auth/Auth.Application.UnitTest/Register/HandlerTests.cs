using Auth.Application.Account.Commands.Register;
using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using Lib.Application.Exceptions;
using Lib.Application.Logging;
using MediatR;
using Moq;
using Shouldly;

namespace Auth.Application.UnitTest.Register
{
    public class HandlerTests
    {
        private readonly Mock<IIdentityService> _identityServiceMock = new();
        private readonly Mock<ILogTrace> _logTraceMock = new();
        private readonly Mock<IPublisher> _publisher = new();

        private readonly RegisterCommand _registerCommand = new()
        {
            Email = "sunlight@yopmail.com",
            Password = "P@ssw0rd",
            FirstName = "First",
            LastName = "Last"
        };

        // setup

        [Fact]
        public async void SucceedsWithNewUserId()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var user = new UserProfileDto
            {
                Id = userId,
                Email = _registerCommand.Email,
                FirstName = _registerCommand.FirstName,
                LastName = _registerCommand.LastName
            };
            var code = "thisIsRegisterConfirmCode";
            _identityServiceMock.Setup(x => x.CreateUserAsync(_registerCommand))
                .ReturnsAsync((user, code));

            // Act
            var handler =
                new RegisterCommandHandler(_publisher.Object, _logTraceMock.Object, _identityServiceMock.Object);
            var result = await handler.Handle(_registerCommand, default);

            // Assert
            result.UserId.ShouldBe(userId);
            _publisher.Verify(c => c.Publish(
                It.Is<RegisterEvent>(x => x.User == user && x.Code == code)
                , It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async void ThrowConflictException_IfEmailIsDuplicate()
        {
            // Arrange
            _identityServiceMock.Setup(x => x.CreateUserAsync(_registerCommand))
                .Throws(new ConflictException($"{_registerCommand.Email} is duplicate!"));

            // Act
            var handler =
                new RegisterCommandHandler(_publisher.Object, _logTraceMock.Object, _identityServiceMock.Object);
            var exception = await Should.ThrowAsync<ConflictException>(() => handler.Handle(_registerCommand, default));

            // Assert
            exception.Message.ShouldBe("{\"message\":\"sunlight@yopmail.com is duplicate!\"}");
            _publisher.Verify(c => c.Publish(
                It.IsAny<RegisterEvent>()
                , It.IsAny<CancellationToken>()), Times.Never());
        }
    }
}