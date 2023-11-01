using Application.Common.Exceptions;
using FluentValidation;
using MediatR;
using System.Dynamic;

namespace Application.Common.Behaviors
{
    internal class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
     where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (_validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);

                var validationResults = await Task.WhenAll(
                    _validators.Select(v =>
                        v.ValidateAsync(context, cancellationToken)));

                var failures = validationResults
                    .Where(r => r.Errors.Any())
                    .SelectMany(r => r.Errors)
                    .ToList();

                if (failures.Any())
                {
                    var obj = new ExpandoObject();
                    failures.ForEach(r =>
                    {
                        obj.TryAdd(char.ToLower(r.PropertyName[0]) + r.PropertyName[1..], r.ErrorMessage);
                    });
                    throw new BadRequestException(obj);
                }
            }
            return await next();
        }
    }
}
