using Auth.Application.Account.Commands.Register;
using Lib.Application.Exceptions;
using MediatR;
using Shouldly;

namespace Auth.Application.UnitTest.Register
{
    public class ValidatePasswordTests : DependencyInjectionFixture
    {
        private readonly RegisterCommand _command = new()
        {
            Email = "sunligh@yopmail.com",
            FirstName = "First",
            LastName = "Last"
        };

        private readonly ISender _sender;

        public ValidatePasswordTests()
        {
            _sender = GetService<ISender>();
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
            exception.Message.ShouldBe("""{"password":"Password must contain at least one number."}""");
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
            exception.Message.ShouldBe("""{"password":"Password must contain at least one number."}""");
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
            exception.Message.ShouldBe(
                """{"password":"Password must have at least one uppercase (\u0027A\u0027-\u0027Z\u0027)."}""");
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("abcde")]
        public async Task FailsIfTooShortTests(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"password":"Password must be at least 6 characters."}""");
        }

        [Theory]
        [InlineData("abcd@e!ld!kaj9Fd")]
        [InlineData("P@ssW0rd")]
        public async Task SucceedsIfValid(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var validator = new RegisterCommandValidator();
            var result = await validator.ValidateAsync(_command);

            // Assert
            result.Errors.Count.ShouldBe(0);
        }
    }
}