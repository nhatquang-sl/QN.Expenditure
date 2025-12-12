using Auth.Application.Account.Commands.ChangeEmail;
using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using AutoMapper;
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

        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IPublisher> _publisher = new();

        // setup

        [Fact]
        public async void SucceedsWithNewUserId()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var code = "thisIsRegisterConfirmCode";
            var user = new UserProfileDto
            {
                Id = userId,
                Email = "sunlight@old.com",
                FirstName = "FirstName",
                LastName = "LastName"
            };
            _currentUser.Setup(x => x.Id).Returns(userId);
            _currentUser.Setup(x => x.Email).Returns(user.Email);
            _currentUser.Setup(x => x.FirstName).Returns(user.FirstName);
            _currentUser.Setup(x => x.LastName).Returns(user.LastName);
            _currentUser.Setup(x => x.EmailConfirmed).Returns(user.EmailConfirmed);
            _mapper.Setup(x => x.Map<UserProfileDto>(It.IsAny<ICurrentUser>())).Returns(user);
            _identityService.Setup(x => x.ChangeEmail(userId, _command)).ReturnsAsync(code);
            _publisher.Setup(x => x.Publish(new ChangeEmailEvent(It.IsAny<UserAuthDto>(), code, _command.NewEmail),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var handler = new ChangeEmailCommandHandler(_mapper.Object, _publisher.Object, _currentUser.Object,
                _identityService.Object);
            var result = await handler.Handle(_command, default);

            // Assert
            result.ShouldBe("Verification email sent. Please check your email.");
            _identityService.Verify(c => c.ChangeEmail(
                It.Is<string>(x => x == userId)
                , It.Is<ChangeEmailCommand>(x => x == _command)), Times.Once());
            _publisher.Verify(c => c.Publish(
                It.Is<ChangeEmailEvent>(x => x.User == user && x.Code == code && x.NewEmail == _command.NewEmail)
                , It.IsAny<CancellationToken>()), Times.Once());
            _mapper.Verify(c => c.Map<UserProfileDto>(It.Is<ICurrentUser>(x => x.Id == userId)), Times.Once());
        }
    }
}