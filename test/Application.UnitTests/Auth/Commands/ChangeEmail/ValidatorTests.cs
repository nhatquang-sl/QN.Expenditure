using Application.Auth.Commands.ChangeEmail;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.ChangeEmail
{
    public class ValidatorTests : DependencyInjectionFixture
    {
        private ChangeEmailCommand _command = new();
        private readonly ISender _sender;
        public ValidatorTests() : base()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissing()
        {
            // Arrange
            _command.NewEmail = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""newEmail"":""New Email is required.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfInvalid()
        {
            // Arrange
            _command.NewEmail = "sunlighyopmail.com";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""newEmail"":""New Email is invalid.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfReachedMaximumLength()
        {
            // Arrange
            _command.NewEmail = string.Empty;
            for (var i = 0; i < 256; i++) _command.NewEmail += "a";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""newEmail"":""New Email has reached a maximum of 255 characters.""}");
        }
    }

}
