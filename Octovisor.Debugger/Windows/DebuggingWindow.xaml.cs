using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Win32;
using Octovisor.Client;
using Octovisor.Debugger.Popups;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;

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
            this.Processes = new ObservableCollection<string>(client.AvailableProcesses.Select(proc => proc.Name));
            this.PrintInitDetails();
            this.Client.Log += this.PrintLine;
            this.Client.ProcessEnded += this.OnProcessTerminated;
            this.Client.ProcessRegistered += this.OnProcessRegistered;

            if (File.Exists("Resources/syntax_highlight.xshd"))
            {
                using (XmlTextReader reader = new XmlTextReader(File.OpenRead("Resources/syntax_highlight.xshd")))
                    this.Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }

            this.Editor.Text = @"/* Use the 'Client' variable to interact with 
* the debugger octovisor client.
*
* Use the 'Print' method to output objects and 
* values in the debugger console. 
*/

// Your code here...";
        }

        public ObservableCollection<string> Processes { get; private set; }

        private void OnProcessRegistered(RemoteProcess proc)
        {
            this.Processes.Add(proc.Name);
            this.PrintLine($"Registering new remote process \'{proc.Name}\'");
        }

        private void OnProcessTerminated(RemoteProcess proc)
        {
            this.Processes.Remove(proc.Name);
            this.PrintLine($"Terminating remote process \'{proc.Name}\'");
        }

        private void OnMouseDrag(object sender, MouseButtonEventArgs e)
            => this.DragMove();

        private async void OnClose(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = (Button)sender;
                btn.IsHitTestVisible = false;
                btn.Background = Brushes.Gray;

                await this.Client.DisconnectAsync();
            }
            catch(Exception ex)
            {
                ExceptionPopup.ShowException(ex);
            }
            finally
            {
                this.Close();
            }
        }

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

        private static readonly object Locker = new object();
        public void PrintLine(string input)
        {
            lock(Locker)
            {
                TextRange trTime = new TextRange(this.RTBConsole.Document.ContentEnd, this.RTBConsole.Document.ContentEnd)
                {
                    Text = this.FormattedTime()
                };
                trTime.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Coral);

                TextRange trOutput = new TextRange(this.RTBConsole.Document.ContentEnd, this.RTBConsole.Document.ContentEnd)
                {
                    Text = $" {input}\n".Replace("\t", new string(' ', 4))
                };
                trOutput.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.White);

                this.RTBConsole.ScrollToEnd();
            }
        }

        public void PrintException(Exception ex)
        {
            lock (Locker)
            {
                TextRange trTime = new TextRange(this.RTBConsole.Document.ContentEnd, this.RTBConsole.Document.ContentEnd)
                {
                    Text = this.FormattedTime()
                };
                trTime.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Coral);

                TextRange trOutput = new TextRange(this.RTBConsole.Document.ContentEnd, this.RTBConsole.Document.ContentEnd)
                {
                    Text = $" {ex}\n"
                };
                trOutput.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Red);

                this.RTBConsole.ScrollToEnd();
            }
        }

        private void PrintInitDetails()
        {
            string displayProcs = string.Join(",", this.Processes);
            this.PrintLine("Registered on server");
            this.PrintLine($"Connected processes ({this.Processes.Count}): {displayProcs}");
        }

        private async void OnRunCode(object sender, RoutedEventArgs e)
        {
            ExecutionContext ctx = new ExecutionContext(this, this.Client);
            try
            {
                ScriptState state = await CSharpScript.RunAsync(this.Editor.Text, ScriptOptions.Default, ctx);
                if (state?.ReturnValue != null)
                    ctx.Print(state?.ReturnValue);
            }
            catch(Exception ex)
            {
                ctx.Error(ex);
            }
        }

        private void OnLoadScriptFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Filter = "C# Source files (.cs)|*.cs",
                FileName = "script",
                DefaultExt = ".cs"
            };

            bool? result = fileDialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                string code = File.ReadAllText(fileDialog.FileName);
                this.Editor.Text = code;
            }
        }
    }
}
