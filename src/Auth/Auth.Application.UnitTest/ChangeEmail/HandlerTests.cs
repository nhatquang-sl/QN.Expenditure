using Auth.Application.Account.Commands.ChangeEmail;
using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Moq;
using Shouldly;

namespace Auth.Application.UnitTest.ChangeEmail
{
    public class HandlerTests
    {
        private readonly ChangeEmailCommand _command = new("sunlight@yopmail.com");
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly Mock<IIdentityService> _identityService = new();
        private readonly Mock<IPublisher> _publisher = new();

        [Fact]
        public async void SucceedsWithNewUserId()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var code = "thisIsRegisterConfirmCode";
            _currentUser.Setup(x => x.Id).Returns(userId);
            _currentUser.Setup(x => x.Email).Returns("sunlight@old.com");
            _currentUser.Setup(x => x.FirstName).Returns("FirstName");
            _currentUser.Setup(x => x.LastName).Returns("LastName");
            _currentUser.Setup(x => x.EmailConfirmed).Returns(false);
            _identityService.Setup(x => x.ChangeEmail(userId, _command)).ReturnsAsync(code);
            _publisher.Setup(x => x.Publish(It.IsAny<ChangeEmailEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var handler = new ChangeEmailCommandHandler(_publisher.Object, _currentUser.Object, _identityService.Object);
            var result = await handler.Handle(_command, default);

            // Assert
            result.ShouldBe("Verification email sent. Please check your email.");
            _identityService.Verify(c => c.ChangeEmail(
                It.Is<string>(x => x == userId)
                , It.Is<ChangeEmailCommand>(x => x == _command)), Times.Once());
            _publisher.Verify(c => c.Publish(
                It.Is<ChangeEmailEvent>(x => x.User.Id == userId && x.Code == code && x.NewEmail == _command.NewEmail)
                , It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}
