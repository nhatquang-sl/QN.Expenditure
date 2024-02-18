using Application.Auth.Commands.Register;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.Register
{
    public class ValidatorTests : DependencyInjectionFixture
    {
        private RegisterCommand _command = new();
        private readonly ISender _sender;
        public ValidatorTests() : base()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async void ThrowBadRequestException_IfCommandIsEmpty()
        {
            // Arrange

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""email"":""Email is required."",""password"":""Password is required."",""firstName"":""First Name is required."",""lastName"":""Last Name is required.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingFirstAndLastName()
        {
            // Arrange
            _command.Email = "sunlight@yopmail.com";
            _command.Password = "P@ssW0rd";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""firstName"":""First Name is required."",""lastName"":""Last Name is required.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfInvalidEmailAndPassword()
        {
            // Arrange
            _command.FirstName = "first";
            _command.LastName = "last";
            _command.Email = "sunlightyopmail.com";
            _command.Password = "password";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""email"":""Email is invalid."",""password"":""Password must have at least one uppercase (\u0027A\u0027-\u0027Z\u0027).""}");
        }
    }
}
