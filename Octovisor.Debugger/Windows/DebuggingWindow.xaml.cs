using Octovisor.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Octovisor.Debugger.Windows
{
    /// <summary>
    /// Interaction logic for DebuggingWindow.xaml
    /// </summary>
    public partial class DebuggingWindow : Window
    {
        private readonly OctoClient Client;
       
        public DebuggingWindow(OctoClient client)
        {
            this.InitializeComponent();
            this.Client = client;
            this.Processes = new ObservableCollection<RemoteProcess>(client.AvailableProcesses);
            this.PrintInitDetails();
            this.Client.Log += this.PrintLine;
            this.Client.ProcessEnded += this.OnProcessTerminated;
            this.Client.ProcessRegistered += this.OnProcessRegistered;
        }

        private void OnProcessRegistered(RemoteProcess proc)
        {
            this.Processes.Add(proc);
            this.PrintLine($"Registering new remote process \'{proc.Name}\'");
        }

        private void OnProcessTerminated(RemoteProcess proc)
        {
            this.Processes.Remove(proc);
            this.PrintLine($"Terminating remote process \'{proc.Name}\'");
        }

        public ObservableCollection<RemoteProcess> Processes { get; private set; }

        private void OnMouseDrag(object sender, MouseButtonEventArgs e)
            => this.DragMove();

        private void OnClose(object sender, RoutedEventArgs e)
            => this.Close();

        private void OnMaximize(object sender, RoutedEventArgs e)
            => this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnMinimize(object sender, RoutedEventArgs e)
            => this.WindowState = WindowState.Minimized;

        private string FormattedTime()
        {
            int hour = DateTime.Now.TimeOfDay.Hours;
            int minute = DateTime.Now.TimeOfDay.Minutes;
            string niceHour = hour < 10 ? "0" + hour : hour.ToString();
            string niceMin = minute < 10 ? "0" + minute : minute.ToString();
            return $"{niceHour}:{niceMin}";
        }

        private void PrintLine(string input)
            => this.RTBConsole.AppendText($"{this.FormattedTime()} {input}\n".Replace("\t", new string(' ', 4)));

        private void PrintInitDetails()
        {
            List<RemoteProcess> procs = this.Client.AvailableProcesses;
            string displayProcs = string.Join(",", procs.Select(proc => proc.Name).ToArray());
            this.PrintLine("Registered on server");
            this.PrintLine($"Connected processes ({procs.Count}):\n{displayProcs}");
        }
    }
}
