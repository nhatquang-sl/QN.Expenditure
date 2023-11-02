using Application.Auth.Commands.Login;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.Login
{
    public class ValidateEmailTests : DependencyInjectionFixture
    {
        private LoginCommand _command = new()
        {
            Password = "P@ssw0rd"
        };
        private readonly ISender _sender;
        public ValidateEmailTests() : base()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissing()
        {
            // Arrange
            _command.Email = "";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfInvalid()
        {
            // Arrange
            _command.Email = "sunlighyopmail.com";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfReachedMaximumLength()
        {
            // Arrange
            _command.Email = "";
            for (var i = 0; i < 256; i++) _command.Email += "a";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }
    }
}
