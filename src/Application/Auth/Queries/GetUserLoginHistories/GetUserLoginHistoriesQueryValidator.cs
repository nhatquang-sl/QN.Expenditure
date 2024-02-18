using FluentValidation;

namespace Application.Auth.Queries.GetUserLoginHistories
{
    public class GetUserLoginHistoriesQueryValidator : AbstractValidator<GetUserLoginHistoriesQuery>
    {
        public GetUserLoginHistoriesQueryValidator()
        {
            RuleFor(v => v.Page)
                .GreaterThanOrEqualTo(1);

            RuleFor(v => v.Size)
                .GreaterThanOrEqualTo(10);
        }
    }
}
