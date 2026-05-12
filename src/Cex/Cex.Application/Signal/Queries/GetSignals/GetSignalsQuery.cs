using Cex.Domain.Enums;
using Lib.Application.Models;
using MediatR;

namespace Cex.Application.Signals.Queries.GetSignals;

public record GetSignalsQuery(
    DateTime From,
    DateTime To,
    string? Interval = null,
    SignalType? SignalType = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PaginatedList<SignalDto>>;
