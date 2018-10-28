using System;
using System.IO;

namespace Octovisor.Server
{
    public class OctovisorLogger
    {
        /// <summary>
        /// Occurs when the logger tries to log something.
        /// </summary>
        public event Action<DateTime, string, string> Log;

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="T:Octovisor.Server.OctovisorLogger"/> should log.
        /// </summary>
        public bool ShouldLog { get; set; } = true;

        private void CallLogEvent(DateTime time, string head, string content)
            => this.Log?.Invoke(time, head, content);

        private string FormatTime(DateTime time)
            => $"{time.TimeOfDay.Hours:00}:{time.TimeOfDay.Minutes:00}";

        private void Prefix(DateTime time)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("> ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"{this.FormatTime(time)} - ");
        }

        private void SaveToFile(DateTime time, string head, string content)
            => File.AppendAllText("logs.txt", $"{this.FormatTime(time)} - [{head.ToUpper()}] >> {content}\n\n");


        public void Write(ConsoleColor col, string head, string content)
        {
            if (this.ShouldLog)
            {
                DateTime now = DateTime.Now;
                this.Prefix(now);

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("[");
                Console.ForegroundColor = col;
                Console.Write(head);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("] >> ");
                Console.WriteLine(content);

                this.SaveToFile(now, head, content);
            }
        }

        internal void Debug(string content) => this.Write(ConsoleColor.Cyan, "Debug", content);

        internal void Warn(string content)
        {
            if (this.ShouldLog)
            {
                DateTime now = DateTime.Now;
                this.Prefix(now);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARN] >> {content}");

                this.SaveToFile(now, "WARN", content);
            }
        }

        internal void Error(string content)
        {
            if (this.ShouldLog)
            {
                DateTime now = DateTime.Now;
                this.Prefix(now);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] >> {content}");

                this.SaveToFile(now, "ERROR", content);
            }
        }

        public void Read() => Console.Read();
    }
}
