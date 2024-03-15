using Application.Common.Exceptions;
using FluentValidation;
using MediatR;
using System.Dynamic;

namespace Application.Common.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
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

                if (failures.Count != 0)
                {
                    var errors = failures.GroupBy(r => r.PropertyName).Select(r => new { PropertyName = r.Key, messages = r.Select(x => x.ErrorMessage).Distinct().ToList() }).ToList();
                    if (errors.Count == 1 && string.IsNullOrWhiteSpace(errors[0].PropertyName) && errors[0].messages.Count == 1)
                    {
                        throw new BadRequestException(errors[0].messages[0]);
                    }

                    var obj = new ExpandoObject();
                    errors.ForEach(r =>
                    {
                        obj.TryAdd(char.ToLower(r.PropertyName[0]) + r.PropertyName[1..], r.messages.FirstOrDefault());
                    });
                    throw new BadRequestException(obj);
                }
            }
            return await next();
        }
    }
}
