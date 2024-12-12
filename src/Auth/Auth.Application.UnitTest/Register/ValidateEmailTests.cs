using Auth.Application.Account.Commands.Register;
using Lib.Application.Exceptions;
using MediatR;
using Shouldly;

namespace Auth.Application.UnitTest.Register
{
    public class ValidateEmailTests : DependencyInjectionFixture
    {
        private readonly RegisterCommand _command = new()
        {
            Password = "P@ssw0rd",
            FirstName = "First",
            LastName = "Last"
        };

        private readonly ISender _sender;

        public ValidateEmailTests()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissing()
        {
            // Arrange
            _command.Email = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"email":"Email is required."}""");
        }

        [Fact]
        public async void ThrowBadRequestException_IfInvalid()
        {
            // Arrange
            _command.Email = "sunlighyopmail.com";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"email":"Email is invalid."}""");
        }

        [Fact]
        public async void ThrowBadRequestException_IfReachedMaximumLength()
        {
            // Arrange
            _command.Email = string.Empty;
            for (var i = 0; i < 256; i++)
            {
                _command.Email += "a";
            }

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"email":"Email has reached a maximum of 255 characters."}""");
        }
    }
}