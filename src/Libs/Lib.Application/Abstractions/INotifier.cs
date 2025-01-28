namespace Lib.Application.Abstractions
{
    public interface INotifier
    {
        Task NotifyInfo(string title, string description,
            CancellationToken cancellationToken = default);

        Task NotifyError(string title, object data,
            CancellationToken cancellationToken = default);
    }
}