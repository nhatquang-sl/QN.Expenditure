using Application.Auth.Commands.ChangePassword;
using Application.Common.Abstractions;
using Moq;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.ChangePassword
{
    public class HandlerTests
    {
        private ChangePasswordCommand _command = new()
        {
            OldPassword = "123456x@X",
            NewPassword = "123456x@X",
            ConfirmPassword = "123456x@X"
        };

        private readonly Mock<ICurrentUser> _currentUserMock;
        private readonly Mock<IIdentityService> _identityServiceMock;

        // setup
        public HandlerTests()
        {
            _currentUserMock = new Mock<ICurrentUser>();
            _identityServiceMock = new Mock<IIdentityService>();
        }

        [Fact]
        public async void SucceedsWithMessage()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            _currentUserMock.Setup(x => x.Id).Returns(userId);
            _identityServiceMock.Setup(x => x.ChangePassword(userId, _command))
                .ReturnsAsync("Your password has been changed.");

            // Act
            var handler = new ChangePasswordCommandHandler(_currentUserMock.Object, _identityServiceMock.Object);
            var result = await handler.Handle(_command, default);

            // Assert
            result.ShouldBe("Your password has been changed.");
            _identityServiceMock.Verify(c => c.ChangePassword(
                It.Is<string>(x => x == userId)
                , It.Is<ChangePasswordCommand>(x => x == _command)), Times.Once());
        }
    }
}
