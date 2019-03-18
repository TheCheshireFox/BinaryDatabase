using System;

namespace Utility
{
    public class LoggerInterface
    {
        public Action<string> LogInfo { get; set; } = s => { };
        public Action<string> LogWarning { get; set; } = s => { };
    }
}
