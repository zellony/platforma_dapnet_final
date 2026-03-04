using System;
using System.Windows;

namespace LicenseGeneratorGUI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try 
        {
            var loginWindow = new LoginWindow();
            
            // Jeśli użytkownik zamknie okno logowania (X) lub kliknie Wyjdź
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            // Jeśli DialogResult == true (poprawne hasło)
            var mainWindow = new MainWindow();
            
            // Przywracamy normalny tryb zamykania (zamknięcie MainWindow zamyka program)
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            this.MainWindow = mainWindow;
            
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd krytyczny podczas startu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
