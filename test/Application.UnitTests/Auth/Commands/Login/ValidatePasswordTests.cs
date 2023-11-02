using Application.Auth.Commands.Login;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.Login
{
    public class ValidatePasswordTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;
        private LoginCommand _command = new()
        {
            Email = "sunligh@yopmail.com",
            Password = ""
        };
        public ValidatePasswordTests() : base()
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
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }

        [Theory]
        [InlineData("abcdef")]
        [InlineData("aaaaaaaaaaa")]
        public async Task FailsIfMissingUpper(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }

        [Theory]
        [InlineData("abCdef")]
        [InlineData("aaAaaaaaaaa")]
        public async Task FailsIfMissingNumber(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
        }

        [Theory]
        [InlineData("abCdef")]
        [InlineData("aaAaaaaaaaa")]
        public async Task FailsIfMissingNonAlphaNumeric(string input)
        {
            // Arrange
            _command.Password = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Email or Password incorrect.""}");
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
            result.Errors.Any().ShouldBeFalse();
        }
    }
}
