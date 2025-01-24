namespace Lib.Application.Abstractions
{
    public interface INotifier
    {
        void Notify(string title, string description, object data);
    }
}