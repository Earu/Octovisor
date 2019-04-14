using System;
using System.Collections.Generic;
using System.IO;

namespace Octovisor.Server
{
    internal class Logger
    {
        internal static object Locker = new object();

        private static readonly string Prefix = "> ";
        private static readonly string Path = "logs";

        private readonly string _MainLogFile;

        internal Logger()
        {
            if (!Directory.Exists(Path))
                Directory.CreateDirectory(Path);
            this._MainLogFile = "octovisor.log";
        }

        private string FormattedTime()
        {
            int hour = DateTime.Now.TimeOfDay.Hours;
            int minute = DateTime.Now.TimeOfDay.Minutes;
            string niceHour = hour < 10 ? "0" + hour : hour.ToString();
            string niceMin = minute < 10 ? "0" + minute : minute.ToString();
            return $"{niceHour}:{niceMin} - ";
        }

        private void Head()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write(Prefix);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(this.FormattedTime());
        }

        internal void LogTo(string fileName, string msg)
        {
            lock(Locker)
            {
                File.AppendAllText($"{Path}/{fileName}", $"{DateTime.Now} - {msg}\n");
            }
        }

        public void Normal(string msg)
        {
            this.Head();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(msg);
            this.LogTo(this._MainLogFile, msg);
        }

        public void Nice(string head, ConsoleColor col, string content)
        {
            this.Head();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("[");
            Console.ForegroundColor = col;
            Console.Write(head);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("] >> ");
            Console.WriteLine(content);
            this.LogTo(this._MainLogFile, $"[{head.ToUpper()}] >> {content}");
        }

        public void Warning(string msg)
        {
            this.Head();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(msg);
            this.LogTo(this._MainLogFile, $"[WARN] >> {msg}");
        }

        public void Danger(string msg)
        {
            this.Head();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            this.LogTo(this._MainLogFile, $"[DANGER] >> {msg}");
        }

        public void Danger(Exception ex)
        {
            this.Head();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex);
            this.LogTo(this._MainLogFile, $"[DANGER] >> {ex}");
        }

        public void Error(string msg)
        {
            this.Head();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("/!\\ ERROR /!\\");
            Console.WriteLine(msg);
            Console.ReadLine();
            this.LogTo(this._MainLogFile, $"[ERROR] >> {msg}");
        }

        public void Good(string msg)
        {
            this.Head();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(msg);
            this.LogTo(this._MainLogFile, $"[GOOD] >> {msg}");
        }
    }
}