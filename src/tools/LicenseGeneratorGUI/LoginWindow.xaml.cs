using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace LicenseGeneratorGUI;

public partial class LoginWindow : Window
{
    // ✅ ZABEZPIECZONY HASH HASŁA (SHA-256)
    // Oryginał: GreenIsTheBest
    private const string SecureHash = "f8fb165db9480fbe650c8e50c23374d6882f8df8550510e3b213afae41f48275";

    public LoginWindow()
    {
        InitializeComponent();
        TxtPassword.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        if (Verify(TxtPassword.Password))
        {
            this.DialogResult = true;
        }
        else
        {
            CustomMessageBox.Show("Błędne hasło dostępu!", "Błąd", CustomMessageBox.MessageBoxType.Error);
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
    }

    private bool Verify(string input)
    {
        using var sha = SHA256.Create();
        var b = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var h = BitConverter.ToString(b).Replace("-", "").ToLower();
        
        // Hash dla "GreenIsTheBest" to: f8fb165db9480fbe650c8e50c23374d6882f8df8550510e3b213afae41f48275
        return h == "f8fb165db9480fbe650c8e50c23374d6882f8df8550510e3b213afae41f48275";
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
