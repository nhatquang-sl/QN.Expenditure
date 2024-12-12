using FluentValidation;
using Lib.Application.Exceptions;
using MediatR;

namespace Lib.Application.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!validators.Any())
            {
                return await next();
            }

            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                validators.Select(v =>
                    v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .Where(r => r.Errors.Count != 0)
                .SelectMany(r => r.Errors)
                .ToList();

            if (failures.Count == 0)
            {
                return await next();
            }

            var badRequest = failures.FirstOrDefault(x => x.PropertyName == string.Empty);
            if (badRequest != null)
            {
                throw new BadRequestException(badRequest.ErrorMessage);
            }

            var errors = failures.GroupBy(r => r.PropertyName).Select(r =>
                new UnprocessableEntity
                {
                    Name = char.ToLowerInvariant(r.Key[0]) + r.Key[1..],
                    Errors = r.Select(x => x.ErrorMessage).ToArray()
                }).ToArray();
            throw new UnprocessableEntityException(errors);
        }
    }
}