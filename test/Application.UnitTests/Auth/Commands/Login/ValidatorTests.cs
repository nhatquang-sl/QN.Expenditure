using Application.Auth.Commands.Login;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.Login
{
    public class ValidatorTests : DependencyInjectionFixture
    {
        private LoginCommand _command = new()
        {
            Email = "sunlight@yopmail.com",
            Password = "P@ssw0rd",
        };
        private readonly ISender _sender;
        public ValidatorTests() : base()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async Task SucceedsIfValid()
        {
            // Arrange

            // Act
            var validator = new LoginCommandValidator();
            var result = await validator.ValidateAsync(_command);

            // Assert
            result.Errors.Any().ShouldBeFalse();
        }

        [Fact]
        public async void ThrowBadRequestException_IfCommandIsEmpty()
        {
            // Arrange
            _command = new();

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingEmail()
        {
            // Arrange
            _command.Email = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingPassword()
        {
            // Arrange
            _command.Password = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }
    }
}
