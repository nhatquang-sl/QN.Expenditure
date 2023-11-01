using Application.Auth.Commands.Register;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.Register
{
    public class ValidateEmailTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;
        public ValidateEmailTests() : base()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissing()
        {
            // Arrange
            var command = new RegisterCommand()
            {
                Password = "P@ssw0rd",
                FirstName = "First",
                LastName = "Last"
            };

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":{""email"":""Email is required.""}}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfInvalid()
        {
            // Arrange
            var command = new RegisterCommand()
            {
                Email = "sunlighyopmail.com",
                Password = "P@ssw0rd",
                FirstName = "First",
                LastName = "Last"
            };

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":{""email"":""Email is invalid.""}}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfReachedMaximumLength()
        {
            // Arrange
            var command = new RegisterCommand()
            {
                Email = "",
                Password = "P@ssw0rd",
                FirstName = "First",
                LastName = "Last"
            };
            for (var i = 0; i < 256; i++) command.Email += "a";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":{""email"":""Email has reached a maximum of 255 characters.""}}");
        }
    }
}
