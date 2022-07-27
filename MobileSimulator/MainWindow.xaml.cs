using System;
using System.Windows;
using MobileSimulator.ViewModels.ViewModels;

namespace MobileSimulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnActivated(object sender, EventArgs e)
        {
            // Show overlay if we lose focus
            var mainWindowViewModel = DataContext as MainWindowViewModel;
            if (mainWindowViewModel != null && mainWindowViewModel.DimmableOverlayVisible)
            {

                mainWindowViewModel.DimmableOverlayVisible = false;
            }
        }

        private void MainWindow_OnDeactivated(object sender, EventArgs e)
        {
            if (Application.Current.Windows.Count > 1)
            {
                // Show overlay if we lose focus
                var mainWindowViewModel = DataContext as MainWindowViewModel;
                if (mainWindowViewModel != null)
                    mainWindowViewModel.DimmableOverlayVisible = true;
            }
        }
    }
}
