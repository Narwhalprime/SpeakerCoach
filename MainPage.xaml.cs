using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace LightBuzz.Vituvius.Samples.WPF
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void Angle_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AnglePage());
        }

        private void Face_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new FacePage());
        }
    }
}
