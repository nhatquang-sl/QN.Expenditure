using Auth.Application.Account.Commands.Login;
using Lib.Application.Exceptions;
using MediatR;
using Shouldly;

namespace Auth.Application.UnitTest.Login
{
    public class ValidatorTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;

        private LoginCommand _command = new()
        {
            Email = "sunlight@yopmail.com",
            Password = "P@ssw0rd"
        };

        public ValidatorTests()
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
            result.Errors.Count.ShouldBe(0);
        }

        [Fact]
        public async void ThrowBadRequestException_IfCommandIsEmpty()
        {
            // Arrange
            _command = new LoginCommand();

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"message":"Email or Password incorrect."}""");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingEmail()
        {
            // Arrange
            _command.Email = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"message":"Email or Password incorrect."}""");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingPassword()
        {
            // Arrange
            _command.Password = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"message":"Email or Password incorrect."}""");
        }
    }
}