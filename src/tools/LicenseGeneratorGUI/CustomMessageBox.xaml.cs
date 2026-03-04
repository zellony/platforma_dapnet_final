using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LicenseGeneratorGUI;

public partial class CustomMessageBox : Window
{
    public enum MessageBoxType { Info, Error, Question }
    public enum MessageBoxResult { Ok, Yes, No }
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.Ok;

    public CustomMessageBox(string message, string title, MessageBoxType type)
    {
        InitializeComponent();
        TxtMessage.Text = message;
        TxtTitle.Text = title.ToUpper();

        // ✅ STONOWANA CZERWIEŃ DLA BŁĘDÓW/BLOKAD
        if (type == MessageBoxType.Error) 
            TxtTitle.Foreground = new SolidColorBrush(Color.FromRgb(169, 50, 38)); // #A93226
        
        if (type == MessageBoxType.Question) 
            TxtTitle.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // #27AE60

        CreateButtons(type);
    }

    private void CreateButtons(MessageBoxType type)
    {
        if (type == MessageBoxType.Question)
        {
            AddButton("TAK", MessageBoxResult.Yes, true);
            AddButton("NIE", MessageBoxResult.No, false);
        }
        else
        {
            AddButton("OK", MessageBoxResult.Ok, true);
        }
    }

    private void AddButton(string text, MessageBoxResult result, bool isPrimary)
    {
        var btn = new Button
        {
            Content = text,
            Width = 80,
            Height = 30,
            Margin = new Thickness(10, 0, 0, 0),
            Background = isPrimary ? new SolidColorBrush(Color.FromRgb(63, 114, 175)) : Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(208, 208, 208)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(63, 78, 93)),
            BorderThickness = new Thickness(1)
        };
        btn.Click += (s, e) => { Result = result; DialogResult = true; Close(); };
        PanelButtons.Children.Add(btn);
    }

    public static MessageBoxResult Show(string message, string title = "Powiadomienie", MessageBoxType type = MessageBoxType.Info)
    {
        var msg = new CustomMessageBox(message, title, type);
        msg.Owner = Application.Current.MainWindow;
        msg.ShowDialog();
        return msg.Result;
    }
}
