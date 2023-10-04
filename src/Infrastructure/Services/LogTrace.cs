using Application.Common.Logging;

namespace Infrastructure.Services
{
    public class LogTrace : LogTraceBase
    {
        public override void Flush()
        {
            Console.WriteLine(_entries);
        }
    }
}
