using System;
using System.Windows;
using System.Windows.Input;

namespace Octovisor.Debugger.Popups
{
    /// <summary>
    /// Interaction logic for ExceptionPopup.xaml
    /// </summary>
    public partial class ExceptionPopup : Window
    {
        private readonly static string[] Memes = new string[]
        {
            "Shit happened.",
            "Shit's on fire yo.",
            "Oopsie Whoopsie Uwu",
            "This is fine.",
            "Nah, not doing this.",
            ":(",
            "Hello, this is Patrick."
        };

        public ExceptionPopup()
        {
            this.InitializeComponent();
        }

        private void OnMouseDrag(object sender, MouseButtonEventArgs e)
            => this.DragMove();

        private void OnClose(object sender, RoutedEventArgs e)
            => this.Close();

        private static string GetRandomMeme()
        {
            Random rand = new Random();
            return Memes[rand.Next(0, Memes.Length)];
        }

        public static void ShowException(Exception ex)
        {
            ExceptionPopup popup = new ExceptionPopup();
            popup.TBMeme.Text = GetRandomMeme();
            popup.TBException.Text = ex.Message;
            popup.ShowDialog();
        }
    }
}
