using System.Windows;
using MobileSimulator.Infrastructure.Interfaces;
using MobileSimulator.Infrastructure.Ioc;
using MobileSimulator.ViewModels.ViewModels;

namespace MobileSimulator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Window MainStartWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            log4net.Config.XmlConfigurator.Configure();

            LoadAdditionalResourceDictionaries();
            MainStartWindow = new MainWindow();
            var context = new MainWindowViewModel();
            MainStartWindow.DataContext = context;
            MainStartWindow.Show();
        }

        private void LoadAdditionalResourceDictionaries()
        {
            var loaders = MefLoader.GetExportedValues<IAdditionalResourceLoader>();
            foreach (var loader in loaders)
            {
                loader.Load(Resources.MergedDictionaries);
            }
        }
    }
}
