using System;
using System.IO;

namespace Octovisor.Server
{
    internal class OctovisorLogger
    {
        private void Prefix()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("> ");
            Console.ForegroundColor = ConsoleColor.Gray;
            int hour = DateTime.Now.TimeOfDay.Hours;
            int minute = DateTime.Now.TimeOfDay.Minutes;
            string nicehour = hour < 10 ? "0" + hour : hour.ToString();
            string nicemin = minute < 10 ? "0" + minute : minute.ToString();
            Console.Write($"{nicehour}:{nicemin} - ");
        }

        private void SaveToFile(string head, string content)
            => File.AppendAllText("logs.txt", $"[{head.ToUpper()}] >> {content}\n\n");


        internal void Log(ConsoleColor col, string head, string content)
        {
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("[");
            Console.ForegroundColor = col;
            Console.Write(head);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("] >> ");
            Console.WriteLine(content);

            this.SaveToFile(head, content);
        }

        internal void Debug(string content) => this.Log(ConsoleColor.Cyan, "Debug", content);

        internal void Warning(string content)
        {
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARN] >> {content}");

            this.SaveToFile("WARN", content);
        }

        internal void Error(string content)
        {
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] >> {content}");

            this.SaveToFile("ERROR", content);
        }

        internal void Pause() => Console.Read();
    }
}
