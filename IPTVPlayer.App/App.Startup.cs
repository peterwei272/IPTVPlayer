using System.Windows;

namespace IPTVPlayer.App
{
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var window = new ShellWindow();
            window.Show();
        }
    }
}
