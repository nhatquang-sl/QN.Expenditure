using Application.Auth.Commands.ChangePassword;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.ChangePassword
{
    public class ValidatePasswordTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;
        private ChangePasswordCommand _command = new()
        {
            OldPassword = "123456x@X",
            NewPassword = "123456x@X",
            ConfirmPassword = "123456x@X"
        };
        public ValidatePasswordTests() : base()
        {
            _sender = GetService<ISender>();
        }

        [Theory]
        [InlineData("abCdef")]
        [InlineData("aaAaaaaaaaa")]
        public async Task FailsIfMissingNonAlphaNumeric(string input)
        {
            // Arrange
            _command.NewPassword = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""password"":""Password must contain at least one number."",""confirmPassword"":""Confirm Password and New Password do not match.""}");
        }

        [Theory]
        [InlineData("abCdef")]
        [InlineData("aaAaaaaaaaa")]
        public async Task FailsIfMissingNumber(string input)
        {
            // Arrange
            _command.NewPassword = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""password"":""Password must contain at least one number."",""confirmPassword"":""Confirm Password and New Password do not match.""}");
        }

        [Theory]
        [InlineData("abcdef")]
        [InlineData("aaaaaaaaaaa")]
        public async Task FailsIfMissingUpper(string input)
        {
            // Arrange
            _command.NewPassword = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""password"":""Password must have at least one uppercase (\u0027A\u0027-\u0027Z\u0027)."",""confirmPassword"":""Confirm Password and New Password do not match.""}");
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("abcde")]
        public async Task FailsIfTooShortTests(string input)
        {
            // Arrange
            _command.NewPassword = input;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""password"":""Password must be at least 6 characters."",""confirmPassword"":""Confirm Password and New Password do not match.""}");
        }

        [Theory]
        [InlineData("abcd@e!ld!kaj9Fd")]
        [InlineData("P@ssW0rd")]
        public async Task FailsIfNotMatch(string input)
        {
            // Arrange
            _command.NewPassword = input;
            _command.ConfirmPassword = input + "@";

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""confirmPassword"":""Confirm Password and New Password do not match.""}");
        }

        [Theory]
        [InlineData("abcd@e!ld!kaj9Fd")]
        [InlineData("P@ssW0rd")]
        public async Task SucceedsIfValid(string input)
        {
            // Arrange
            _command.NewPassword = input;
            _command.ConfirmPassword = input;

            // Act
            var validator = new ChangePasswordCommandValidator();
            var result = await validator.ValidateAsync(_command);

            // Assert
            result.Errors.Any().ShouldBeFalse();
        }
    }
}
