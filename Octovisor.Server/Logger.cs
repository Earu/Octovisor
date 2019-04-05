using System;
using System.Collections.Generic;
using System.IO;

namespace Octovisor.Server
{
    internal class Logger
    {
        private static readonly string _Prefix = "> ";
        private static readonly string _Path = "logs";

        private readonly string _MainLogFile;

        internal Logger()
        {
            if (!Directory.Exists(_Path))
                Directory.CreateDirectory(_Path);
            this._MainLogFile = "octovisor.log";
        }

        private string FormattedTime()
        {
            int hour = DateTime.Now.TimeOfDay.Hours;
            int minute = DateTime.Now.TimeOfDay.Minutes;
            string nicehour = hour < 10 ? "0" + hour : hour.ToString();
            string nicemin = minute < 10 ? "0" + minute : minute.ToString();
            return $"{nicehour}:{nicemin} - ";
        }

        private void Prefix()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write(_Prefix);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(this.FormattedTime());
        }

        internal void LogTo(string filename, string msg)
            => File.AppendAllText($"{_Path}/{filename}", $"{DateTime.Now} - {msg}\n");

        public void Normal(string msg)
        {
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(msg);
            this.LogTo(this._MainLogFile, msg);
        }

        public void Nice(string head, ConsoleColor col, string content)
        {
            this.Prefix();
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
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(msg);
            this.LogTo(this._MainLogFile, $"[WARN] >> {msg}");
        }

        public void Danger(string msg)
        {
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            this.LogTo(this._MainLogFile, $"[DANGER] >> {msg}");
        }

        public void Danger(Exception ex)
        {
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex);
            this.LogTo(this._MainLogFile, $"[DANGER] >> {ex}");
        }

        public void Error(string msg)
        {
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("/!\\ ERROR /!\\");
            Console.WriteLine(msg);
            Console.ReadLine();
            this.LogTo(this._MainLogFile, $"[ERROR] >> {msg}");
        }

        public void Good(string msg)
        {
            this.Prefix();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(msg);
            this.LogTo(this._MainLogFile, $"[GOOD] >> {msg}");
        }

        public void Notify(string msg)
            => Console.WriteLine($"\n\t---------\\\\\\\\ {msg} ////---------\n");

        public static void Debug(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Debug");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(" >> ");
            Console.WriteLine(msg);
        }

        public static void Debug(List<string> msgs)
        {
            foreach (string msg in msgs)
                Debug(msg);
        }
    }
}