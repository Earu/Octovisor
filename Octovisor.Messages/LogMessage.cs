namespace Octovisor.Messages
{
    public enum LogSeverity
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
    }

    public class LogMessage
    {
        public LogMessage(LogSeverity severity, string content)
        {
            this.Severity = severity;
            this.Content = content;
        }

        public LogSeverity Severity { get; private set; }
        public string Content { get; private set; }

    }
}
