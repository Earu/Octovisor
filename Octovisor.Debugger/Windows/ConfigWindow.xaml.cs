using Microsoft.Win32;
using Octovisor.Client;
using Octovisor.Debugger.Popups;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YamlDotNet.Serialization;

namespace Octovisor.Debugger.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ConfigWindow : Window
    {
        public ConfigWindow()
        {
            this.InitializeComponent();
            this.LoadLastConfig();
        }

        private void OnMouseDrag(object sender, MouseButtonEventArgs e)
            => this.DragMove();

        private void OnClose(object sender, RoutedEventArgs e)
            => this.Close();
        
        private void LoadLastConfig()
        {
            if (!File.Exists("last_config.yaml")) return;

            try
            {
                Config config = Config.FromFile("last_config.yaml");
                this.TBAddress.Text = config.Address;
                this.TBPort.Text = config.Port.ToString();
                this.TBProcessName.Text = config.ProcessName;
                this.TBTimeout.Text = config.Timeout.ToString();
                this.TBToken.Password = config.Token;
                this.TBBufferSize.Text = config.BufferSize.ToString();
                this.TBCompressionTreshold.Text = config.CompressionThreshold.ToString();
            }
            catch
            {
                File.Delete("last_config.yaml");
            }
        }

        private void SaveConfig(Config config)
        {
            Serializer serializer = new Serializer();
            string yaml = serializer.Serialize(config);
            File.WriteAllText("last_config.yaml", yaml);
        }

        private void OnConfigFileClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Filter = "YAML Config files (.yaml)|*.yaml",
                FileName = "config",
                DefaultExt = ".yaml"
            };

            bool? result = fileDialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                try
                {
                    Config config = Config.FromFile(fileDialog.FileName);
                    this.SaveConfig(config);

                    this.TBAddress.Text = config.Address;
                    this.TBPort.Text = config.Port.ToString();
                    this.TBProcessName.Text = config.ProcessName;
                    this.TBTimeout.Text = config.Timeout.ToString();
                    this.TBToken.Password = config.Token;
                    this.TBBufferSize.Text = config.BufferSize.ToString();
                    this.TBCompressionTreshold.Text = config.CompressionThreshold.ToString();
                }
                catch(Exception ex)
                {
                    ExceptionPopup.ShowException(ex);
                }
            }
        }

        private async void OnConnect(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            btn.IsHitTestVisible = false;
            btn.Background = Brushes.Gray;
            DebuggingWindow win = null;

            try
            {
                Config config = new Config
                {
                    Address = this.TBAddress.Text,
                    Port = int.Parse(this.TBPort.Text),
                    Token = this.TBToken.Password,
                    ProcessName = this.TBProcessName.Text,
                    BufferSize = int.Parse(this.TBBufferSize.Text),
                    CompressionThreshold = int.Parse(this.TBCompressionTreshold.Text),
                    Timeout = int.Parse(this.TBTimeout.Text),
                };

                this.SaveConfig(config);

                OctoClient client = new OctoClient(config);
                await client.ConnectAsync();

                win = new DebuggingWindow(client);
                win.ShowDialog();

                // In case debugging window is closed with Windows
                if (client.IsRegistered)
                    await client.DisconnectAsync();
            }
            catch(Exception ex)
            {
                ExceptionPopup.ShowException(ex);
                win?.Close();
            }

            btn.IsHitTestVisible = true;
            BrushConverter converter = new BrushConverter();
            btn.Background = (Brush)converter.ConvertFromString("#191919");
        }

        private void OnNumberOnlyTextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            tb.Text = Regex.Replace(tb.Text, "[^0-9]", _ => string.Empty);
            tb.CaretIndex = tb.Text.Length;
        }

        private void OnTokenChanged(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this.TBToken.Password))
                this.TBTokenPlaceholder.Visibility = Visibility.Visible;
            else
                this.TBTokenPlaceholder.Visibility = Visibility.Hidden;
        }
    }
}
