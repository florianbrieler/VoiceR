using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace VoiceR
{
    public sealed partial class TrayMenuControl : UserControl
    {
        public event Action? StartClicked;
        public event Action? ConfigureClicked;
        public event Action? QuitClicked;

        public TrayMenuControl()
        {
            this.InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartClicked?.Invoke();
        }

        private void ConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            ConfigureClicked?.Invoke();
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            QuitClicked?.Invoke();
        }
    }
}

