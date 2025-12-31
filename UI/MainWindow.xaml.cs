using Microsoft.UI.Xaml;
using VoiceR.UI;

namespace VoiceR
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            
            // Set window icon
            IconHelper.SetWindowIcon(this);
        }
    }
}

