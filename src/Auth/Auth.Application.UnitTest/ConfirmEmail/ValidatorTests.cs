using Auth.Application.Account.Commands.ConfirmEmail;
using Lib.Application.Exceptions;
using MediatR;
using Shouldly;

namespace Auth.Application.UnitTest.ConfirmEmail
{
    public class ValidatorTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;
        private ConfirmEmailCommand _command = new(Guid.NewGuid().ToString(), "code");

        public ValidatorTests()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async Task SucceedsIfValid()
        {
            // Arrange

            // Act
            var validator = new ConfirmEmailCommandValidator();
            var result = await validator.ValidateAsync(_command);

            // Assert
            result.Errors.Any().ShouldBeFalse();
        }

        [Fact]
        public async void ThrowBadRequestException_IfCommandIsEmpty()
        {
            // Arrange
            _command = new ConfirmEmailCommand(string.Empty, string.Empty);

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"userId":"UserId is required.","code":"Code is required."}""");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingCode()
        {
            // Arrange
            _command.Code = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"code":"Code is required."}""");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingUserId()
        {
            // Arrange
            _command.UserId = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("""{"userId":"UserId is required."}""");
        }
    }
}