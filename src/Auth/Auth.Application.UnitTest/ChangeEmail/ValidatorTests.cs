using Auth.Application.Account.Commands.ChangeEmail;
using Lib.Application.Exceptions;
using MediatR;
using Shouldly;

namespace Auth.Application.UnitTest.ChangeEmail
{
    public class ValidatorTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;

        public ValidatorTests()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissing()
        {
            // Arrange
            var command = new ChangeEmailCommand(string.Empty);

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(command));

            // Assert
            exception.Message.ShouldBe("""{"newEmail":"New Email is required."}""");
        }

        [Fact]
        public async void ThrowBadRequestException_IfInvalid()
        {
            // Arrange
            var command = new ChangeEmailCommand("sunlighyopmail.com");

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(command));

            // Assert
            exception.Message.ShouldBe("""{"newEmail":"New Email is invalid."}""");
        }

        [Fact]
        public async void ThrowBadRequestException_IfReachedMaximumLength()
        {
            // Arrange
            var email = string.Empty;
            for (var i = 0; i < 256; i++)
            {
                email += "a";
            }

            var command = new ChangeEmailCommand(email);

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(command));

            // Assert
            exception.Message.ShouldBe("""{"newEmail":"New Email has reached a maximum of 255 characters."}""");
        }
    }
}