using Auth.Application.Account.Commands.ChangePassword;
using Lib.Application.Exceptions;
using MediatR;
using Shouldly;

namespace Auth.Application.UnitTest.ChangePassword
{
    public class ValidatePasswordTests : DependencyInjectionFixture
    {
        private const string ValidPassword = "123456x@X";
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
            var command = new ChangePasswordCommand(ValidPassword, input, "a" + input);

            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _sender.Send(command));

            // Assert
            exception.Message.ShouldBe(
                "[{\"name\":\"password\",\"errors\":[\"Password must contain at least one number.\"]},{\"name\":\"confirmPassword\",\"errors\":[\"Confirm Password and New Password do not match.\"]}]");
        }

        [Theory]
        [InlineData("abCdef")]
        [InlineData("aaAaaaaaaaa")]
        public async Task FailsIfMissingNumber(string input)
        {
            // Arrange
            var command = new ChangePasswordCommand(ValidPassword, input, "a" + input);

            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _sender.Send(command));

            // Assert
            exception.Message.ShouldBe(
                "[{\"name\":\"password\",\"errors\":[\"Password must contain at least one number.\"]},{\"name\":\"confirmPassword\",\"errors\":[\"Confirm Password and New Password do not match.\"]}]");
        }

        [Theory]
        [InlineData("abcdef")]
        [InlineData("aaaaaaaaaaa")]
        public async Task FailsIfMissingUpper(string input)
        {
            // Arrange
            var command = new ChangePasswordCommand(ValidPassword, input, "a" + input);

            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _sender.Send(command));

            // Assert
            exception.Message.ShouldBe(
                "[{\"name\":\"password\",\"errors\":[\"Password must have at least one uppercase (\\u0027A\\u0027-\\u0027Z\\u0027).\"]},{\"name\":\"confirmPassword\",\"errors\":[\"Confirm Password and New Password do not match.\"]}]");
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("abcde")]
        public async Task FailsIfTooShortTests(string input)
        {
            // Arrange
            var command = new ChangePasswordCommand(ValidPassword, input, "a" + input);

            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _sender.Send(command));

            // Assert
            exception.Message.ShouldBe(
                "[{\"name\":\"password\",\"errors\":[\"Password must be at least 6 characters.\"]},{\"name\":\"confirmPassword\",\"errors\":[\"Confirm Password and New Password do not match.\"]}]");
        }

        [Theory]
        [InlineData("abcd@e!ld!kaj9Fd")]
        [InlineData("P@ssW0rd")]
        public async Task FailsIfNotMatch(string input)
        {
            // Arrange
            var command = new ChangePasswordCommand(ValidPassword, input, "a" + input);

            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _sender.Send(command));

            // Assert
            exception.Message.ShouldBe(
                "[{\"name\":\"confirmPassword\",\"errors\":[\"Confirm Password and New Password do not match.\"]}]");
        }

        [Theory]
        [InlineData("abcd@e!ld!kaj9Fd")]
        [InlineData("P@ssW0rd")]
        public async Task SucceedsIfValid(string input)
        {
            // Arrange
            var command = new ChangePasswordCommand(ValidPassword, input, input);

            // Act
            var validator = new ChangePasswordCommandValidator();
            var result = await validator.ValidateAsync(command);

            // Assert
            result.Errors.Count.ShouldBe(0);
        }
    }
}