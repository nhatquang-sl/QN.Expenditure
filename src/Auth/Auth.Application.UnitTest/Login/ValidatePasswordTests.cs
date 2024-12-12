using Auth.Application.Account.Commands.Login;
using Lib.Application.Exceptions;
using MediatR;
using Shouldly;

namespace Auth.Application.UnitTest.Login
{
    public class ValidatePasswordTests : DependencyInjectionFixture
    {
        private readonly LoginCommand _command = new()
        {
            Email = "sunligh@yopmail.com"
        };

        private readonly ISender _sender;

        public ValidatePasswordTests()
        {
            _sender = GetService<ISender>();
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("abcde")]
        public async Task FailsIfTooShortTests(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"message":"Email or Password incorrect."}""");
        }

        [Theory]
        [InlineData("abcdef")]
        [InlineData("aaaaaaaaaaa")]
        public async Task FailsIfMissingUpper(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"message":"Email or Password incorrect."}""");
        }

        [Theory]
        [InlineData("abCdef")]
        [InlineData("aaAaaaaaaaa")]
        public async Task FailsIfMissingNumber(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"message":"Email or Password incorrect."}""");
        }

        [Theory]
        [InlineData("abCdef")]
        [InlineData("aaAaaaaaaaa")]
        public async Task FailsIfMissingNonAlphaNumeric(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"message":"Email or Password incorrect."}""");
        }

        [Theory]
        [InlineData("abcd@e!ld!kaj9Fd")]
        [InlineData("P@ssW0rd")]
        public async Task SucceedsIfValid(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var validator = new LoginCommandValidator();
            var result = await validator.ValidateAsync(_command);

            // Assert
            result.Errors.Count.ShouldBe(0);
        }
    }
}