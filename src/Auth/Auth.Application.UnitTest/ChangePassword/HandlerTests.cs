using Auth.Application.Account.Commands.ChangePassword;
using Auth.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Moq;
using Shouldly;

namespace Auth.Application.UnitTest.ChangePassword
{
    public class HandlerTests
    {
        private const string ValidPassword = "123456x@X";

        private readonly Mock<ICurrentUser> _currentUserMock = new();
        private readonly Mock<IIdentityService> _identityServiceMock = new();

        // setup

        [Fact]
        public async void SucceedsWithMessage()
        {
            // Arrange
            var command = new ChangePasswordCommand(ValidPassword, ValidPassword, ValidPassword);
            var userId = Guid.NewGuid().ToString();
            _currentUserMock.Setup(x => x.Id).Returns(userId);
            _identityServiceMock.Setup(x => x.ChangePassword(userId, command))
                .ReturnsAsync("Your password has been changed.");

            // Act
            var handler = new ChangePasswordCommandHandler(_currentUserMock.Object, _identityServiceMock.Object);
            var result = await handler.Handle(command, default);

            // Assert
            result.ShouldBe("Your password has been changed.");
            _identityServiceMock.Verify(c => c.ChangePassword(
                It.Is<string>(x => x == userId)
                , It.Is<ChangePasswordCommand>(x => x == command)), Times.Once());
        }
    }
}