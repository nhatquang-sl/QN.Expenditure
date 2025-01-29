using Auth.Application.Account.Commands.Register;
using Lib.Application.Exceptions;
using MediatR;
using Shouldly;

namespace Auth.Application.UnitTest.Register
{
    public class ValidateLastNameTests : DependencyInjectionFixture
    {
        private readonly RegisterCommand _command = new()
        {
            Email = "sunligh@yopmail.com",
            Password = "123456x@X",
            FirstName = "First",
            LastName = "Last"
        };

        private readonly ISender _sender;

        public ValidateLastNameTests()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async void ThrowUnprocessableEntityException_IfMissing()
        {
            // Arrange
            _command.LastName = string.Empty;

            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe("[{\"name\":\"lastName\",\"errors\":[\"Last Name is required.\"]}]");
        }

        [Fact]
        public async void ThrowUnprocessableEntityException_IfTooLong()
        {
            // Arrange
            _command.LastName = string.Empty;
            for (var i = 0; i < 51; i++)
            {
                _command.LastName += "a";
            }

            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe(
                "[{\"name\":\"lastName\",\"errors\":[\"Last Name has reached a maximum of 50 characters.\"]}]");
        }

        [Theory]
        [InlineData("a")]
        [InlineData("b")]
        public async void ThrowUnprocessableEntityException_IfTooShort(string input)
        {
            // Arrange
            _command.LastName = input;

            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _sender.Send(_command));

            // Assert
            exception.Message.ShouldBe(
                "[{\"name\":\"lastName\",\"errors\":[\"Last Name must be at least 2 characters.\"]}]");
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("P@ss")]
        public async Task SucceedsIfValid(string input)
        {
            // Arrange
            _command.LastName = input;

            // Act
            var validator = new RegisterCommandValidator();
            var result = await validator.ValidateAsync(_command);

            // Assert
            result.Errors.Count.ShouldBe(0);
        }
    }
}