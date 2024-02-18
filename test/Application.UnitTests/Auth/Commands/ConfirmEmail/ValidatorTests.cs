using Application.Auth.Commands.ConfirmEmail;
using Application.Common.Exceptions;
using MediatR;
using Shouldly;

namespace Application.UnitTests.Auth.Commands.ConfirmEmail
{
    public class ValidatorTests : DependencyInjectionFixture
    {
        private ConfirmEmailCommand _command = new(Guid.NewGuid().ToString(), "code");
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
            var validator = new ConfirmEmailCommandValidator();
            var result = await validator.ValidateAsync(_command);

            // Assert
            result.Errors.Any().ShouldBeFalse();
        }

        [Fact]
        public async void ThrowBadRequestException_IfCommandIsEmpty()
        {
            // Arrange
            _command = new(string.Empty, string.Empty);

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""userId"":""UserId is required."",""code"":""Code is required.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingCode()
        {
            // Arrange
            _command.Code = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""code"":""Code is required.""}");
        }

        [Fact]
        public async void ThrowBadRequestException_IfMissingUserId()
        {
            // Arrange
            _command.UserId = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(() => _sender.Send(_command, default));

            // Assert
            exception.Message.ShouldBe(@"{""userId"":""UserId is required.""}");
        }
    }
}
