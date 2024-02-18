using Application.Auth.Commands.Register;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.Register
{
    public class ValidateEmailTests : DependencyInjectionFixture
    {
        private RegisterCommand _command = new()
        {
            Password = "P@ssw0rd",
            FirstName = "First",
            LastName = "Last"
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
            _command.Email = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""email"":""Email is required.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfInvalid()
        {
            // Arrange
            _command.Email = "sunlighyopmail.com";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""email"":""Email is invalid.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfReachedMaximumLength()
        {
            // Arrange
            _command.Email = string.Empty;
            for (var i = 0; i < 256; i++) _command.Email += "a";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""email"":""Email has reached a maximum of 255 characters.""}");
        }
    }
}
