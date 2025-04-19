namespace Lib.Application.Abstractions
{
    public interface INotifier
    {
        Task Notify(string message, CancellationToken cancellationToken = default);

        Task NotifyInfo(string title, string description,
            CancellationToken cancellationToken = default);

        Task NotifyInfo(string description, object data,
            CancellationToken cancellationToken = default);

        Task NotifyError(string description, object data,
            CancellationToken cancellationToken = default);

        Task NotifyError(string description, Exception ex,
            CancellationToken cancellationToken = default);
    }
}