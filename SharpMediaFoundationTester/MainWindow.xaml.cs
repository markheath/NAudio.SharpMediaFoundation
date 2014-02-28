using System.Windows;

namespace SharpMediaFoundationTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            EncodeView.DataContext = new MediaFoundationEncodeViewModel();
            EnumView.DataContext = new EnumMftViewModel();
            PlaybackView.DataContext = new MediaFoundationPlaybackViewModel();
            ResamplerView.DataContext = new MediaFoundationResampleViewModel();
        }
    }
}
