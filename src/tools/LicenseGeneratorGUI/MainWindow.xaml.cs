using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Newtonsoft.Json;
using LicenseGeneratorGUI.Models;
using Microsoft.Data.Sqlite;

namespace LicenseGeneratorGUI;

public partial class MainWindow : Window
{
    private string _privateKey = "";
    private string _publicKey = "";
    private Guid? _editingCompanyId = null;

    // ✅ ZACIEWNIONE HASŁO AES: GreenIsTheKing
    private static readonly byte[] _aesSecret = { 0x47, 0x72, 0x65, 0x65, 0x6e, 0x49, 0x73, 0x54, 0x68, 0x65, 0x4b, 0x69, 0x6e, 0x67 };
    
    public ObservableCollection<Company> Companies { get; } = new();
    public ObservableCollection<ModuleDef> Modules { get; } = new();
    public ObservableCollection<IssuedLicense> Licenses { get; } = new();

    public MainWindow()
    {
        SQLitePCL.Batteries_V2.Init();
        InitializeComponent();
        DgCompanies.ItemsSource = Companies;
        LstModules.ItemsSource = Modules;
        DgLicenses.ItemsSource = Licenses;
        
        try 
        {
            using (var db = new GeneratorDbContext()) { db.Database.EnsureCreated(); }
            LoadData();
            CheckKeys();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show($"Błąd bazy danych: {ex.Message}", "Błąd Krytyczny", CustomMessageBox.MessageBoxType.Error);
        }
    }

    private void LoadData()
    {
        using var db = new GeneratorDbContext();
        var companies = db.Companies.OrderBy(c => c.Name).ToList();
        Companies.Clear(); foreach (var c in companies) Companies.Add(c);
        var modules = db.Modules.OrderBy(m => m.Name).ToList();
        Modules.Clear(); foreach (var m in modules) Modules.Add(m);
        RefreshNewLicenseModules();
    }

    private void RefreshNewLicenseModules()
    {
        StackNewModules.Children.Clear();
        foreach (var m in Modules)
        {
            StackNewModules.Children.Add(new CheckBox { Content = m.Name, Tag = m.Code, IsChecked = true, Margin = new Thickness(0,2,0,2), Foreground = System.Windows.Media.Brushes.LightGray });
        }
    }

    private void CheckKeys()
    {
        using var db = new GeneratorDbContext();
        _privateKey = db.Settings.FirstOrDefault(s => s.Key == "PrivateKey")?.Value ?? "";
        _publicKey = db.Settings.FirstOrDefault(s => s.Key == "PublicKey")?.Value ?? "";
        TxtKeyStatus.Text = (!string.IsNullOrEmpty(_privateKey) && !string.IsNullOrEmpty(_publicKey)) ? "✅ KLUCZE RSA SĄ POPRAWNIE WCZYTANE" : "⚠️ BRAK KOMPLETNYCH KLUCZY W BAZIE";
        TxtKeyStatus.Foreground = (!string.IsNullOrEmpty(_privateKey) && !string.IsNullOrEmpty(_publicKey)) ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)) : System.Windows.Media.Brushes.Orange;
        TxtPrivKeyPreview.Text = string.IsNullOrEmpty(_privateKey) ? "BRAK" : _privateKey.Substring(0, Math.Min(30, _privateKey.Length)) + "...";
        TxtPubKeyPreview.Text = string.IsNullOrEmpty(_publicKey) ? "BRAK" : _publicKey.Substring(0, Math.Min(30, _publicKey.Length)) + "...";
    }

    public void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    public void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    public void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void DgCompanies_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DgCompanies.SelectedItem is Company company)
        {
            GridCompanyDetails.Visibility = Visibility.Visible;
            TxtCompanyName.Text = company.Name;
            TxtCompanyNip.Text = $"NIP: {company.Nip}";
            TxtCompanyAddress.Text = $"{company.Address}\n{company.PostalCode} {company.City}";
            RefreshLicenseHistory(company.Id);
        }
    }

    private void RefreshLicenseHistory(Guid companyId)
    {
        using var db = new GeneratorDbContext();
        var history = db.Licenses.Where(l => l.CompanyId == companyId).OrderByDescending(l => l.IssuedAt).ToList();
        Licenses.Clear(); foreach (var l in history) Licenses.Add(l);
    }

    private void TxtSearchCompany_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = TxtSearchCompany.Text.ToLower();
        using var db = new GeneratorDbContext();
        var filtered = db.Companies.Where(c => c.Name.ToLower().Contains(filter) || c.Nip.Contains(filter)).OrderBy(c => c.Name).ToList();
        Companies.Clear(); foreach (var c in filtered) Companies.Add(c);
    }

    public void BtnShowAddCompany_Click(object sender, RoutedEventArgs e)
    {
        _editingCompanyId = null;
        TxtModalCompanyTitle.Text = "DODAJ NOWĄ FIRMĘ";
        TxtNewNip.Clear(); TxtNewName.Clear(); TxtNewAddress.Clear(); TxtNewZip.Clear(); TxtNewCity.Clear(); TxtNewPhone.Clear(); TxtNewEmail.Clear();
        ModalAddCompany.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(new Action(() => TxtNewNip.Focus()), System.Windows.Threading.DispatcherPriority.Render);
    }

    public void BtnEditCompany_Click(object sender, RoutedEventArgs e)
    {
        if (DgCompanies.SelectedItem is Company company)
        {
            _editingCompanyId = company.Id;
            TxtModalCompanyTitle.Text = "EDYTUJ DANE FIRMY";
            TxtNewNip.Text = company.Nip; TxtNewName.Text = company.Name; TxtNewAddress.Text = company.Address;
            TxtNewZip.Text = company.PostalCode; TxtNewCity.Text = company.City; TxtNewPhone.Text = company.Phone; TxtNewEmail.Text = company.Email;
            ModalAddCompany.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(new Action(() => TxtNewNip.Focus()), System.Windows.Threading.DispatcherPriority.Render);
        }
    }

    public void BtnSaveCompany_Click(object sender, RoutedEventArgs e)
    {
        var nip = TxtNewNip.Text.Trim();
        var name = TxtNewName.Text.Trim();
        if (string.IsNullOrEmpty(nip) || string.IsNullOrEmpty(name)) { CustomMessageBox.Show("NIP i Nazwa są wymagane!", "Błąd walidacji", CustomMessageBox.MessageBoxType.Error); return; }

        using var db = new GeneratorDbContext();
        Company? company;
        if (_editingCompanyId.HasValue) {
            company = db.Companies.Find(_editingCompanyId.Value);
            if (company == null) return;
        } else {
            if (db.Companies.Any(c => c.Nip == nip)) { CustomMessageBox.Show("Firma o tym NIP już istnieje!", "Błąd", CustomMessageBox.MessageBoxType.Error); return; }
            company = new Company { Id = Guid.NewGuid() };
            db.Companies.Add(company);
        }

        company.Nip = nip; company.Name = name; company.Address = TxtNewAddress.Text;
        company.PostalCode = TxtNewZip.Text; company.City = TxtNewCity.Text;
        company.Phone = TxtNewPhone.Text; company.Email = TxtNewEmail.Text;

        db.SaveChanges();
        ModalAddCompany.Visibility = Visibility.Collapsed;
        LoadData();
        DgCompanies.SelectedItem = Companies.FirstOrDefault(c => c.Id == company.Id);
    }

    public void BtnDeleteCompany_Click(object sender, RoutedEventArgs e)
    {
        if (DgCompanies.SelectedItem is Company company)
        {
            using var db = new GeneratorDbContext();
            if (db.Licenses.Any(l => l.CompanyId == company.Id)) { CustomMessageBox.Show("Nie można usunąć firmy z historią licencji!", "Blokada", CustomMessageBox.MessageBoxType.Error); return; }
            if (CustomMessageBox.Show($"Usunąć firmę {company.Name}?", "Potwierdzenie", CustomMessageBox.MessageBoxType.Question) == CustomMessageBox.MessageBoxResult.Yes)
            {
                db.Companies.Remove(db.Companies.Find(company.Id)!);
                db.SaveChanges();
                GridCompanyDetails.Visibility = Visibility.Collapsed;
                LoadData();
            }
        }
    }

    public void BtnShowNewLicenseForm_Click(object sender, RoutedEventArgs e)
    {
        DtpNewExpires.SelectedDate = DateTime.Today.AddYears(1);
        DtpNewUpdateUntil.SelectedDate = DateTime.Today.AddYears(1);
        ModalNewLicense.Visibility = Visibility.Visible;
    }

    public void BtnConfirmGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (DgCompanies.SelectedItem is not Company company) return;
        if (string.IsNullOrEmpty(_privateKey)) { CustomMessageBox.Show("Brak klucza prywatnego!", "Błąd", CustomMessageBox.MessageBoxType.Error); return; }

        try
        {
            var expiresAt = DtpNewExpires.SelectedDate ?? DateTime.Today.AddYears(1);
            var updateUntil = DtpNewUpdateUntil.SelectedDate;
            var seats = int.TryParse(TxtNewSeats.Text, out var s) ? s : 1;
            var modules = StackNewModules.Children.OfType<CheckBox>().Where(cb => cb.IsChecked == true).Select(cb => cb.Tag!.ToString()!).ToList();

            var licenseData = new { Id = Guid.NewGuid(), Nip = company.Nip, IssuedAt = DateTime.UtcNow, ExpiresAt = expiresAt, UpdateUntil = updateUntil, Seats = seats, Modules = modules, Type = "FULL" };
            var json = JsonConvert.SerializeObject(licenseData);

            // ✅ 1. SZYFROWANIE AES-256
            var encryptedPayload = EncryptAes(json);

            // ✅ 2. PODPIS RSA (podpisujemy zaszyfrowany payload)
            using var rsa = new RSACryptoServiceProvider(); rsa.FromXmlString(_privateKey);
            var signature = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(encryptedPayload), CryptoConfig.MapNameToOID("SHA256")!));
            
            // ✅ 3. FINALNY PLIK (Payload + Signature)
            var finalJson = JsonConvert.SerializeObject(new { Payload = encryptedPayload, Signature = signature }, Formatting.Indented);

            using (var db = new GeneratorDbContext())
            {
                db.Licenses.Add(new IssuedLicense { Id = Guid.NewGuid(), CompanyId = company.Id, IssuedAt = DateTime.UtcNow, ExpiresAt = expiresAt, UpdateUntil = updateUntil, Seats = seats, ModulesJson = string.Join(", ", modules), LicenseBlob = finalJson });
                db.SaveChanges();
            }

            ModalNewLicense.Visibility = Visibility.Collapsed;
            RefreshLicenseHistory(company.Id);
            CustomMessageBox.Show("✅ Licencja wygenerowana i zaszyfrowana.");
        }
        catch (Exception ex) { CustomMessageBox.Show(ex.Message, "Błąd", CustomMessageBox.MessageBoxType.Error); }
    }

    private string EncryptAes(string plainText)
    {
        using Aes aes = Aes.Create();
        aes.KeySize = 256;
        // Generujemy klucz z naszego sekretu
        using var deriveBytes = new Rfc2898DeriveBytes(_aesSecret, new byte[] { 0x44, 0x41, 0x50, 0x4e, 0x45, 0x54, 0x5f, 0x53, 0x41, 0x4c, 0x54 }, 1000, HashAlgorithmName.SHA256);
        aes.Key = deriveBytes.GetBytes(32);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length); // Zapisujemy IV na początku strumienia
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    public void BtnShowLicenseInfo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            using var db = new GeneratorDbContext();
            var lic = db.Licenses.Find(id);
            if (lic != null)
            {
                TxtDetId.Text = lic.Id.ToString();
                TxtDetIssued.Text = lic.IssuedAt.ToString("yyyy-MM-dd HH:mm");
                TxtDetExpires.Text = lic.ExpiresAt.ToString("yyyy-MM-dd");
                TxtDetSeats.Text = lic.Seats.ToString();
                TxtDetUpdate.Text = lic.UpdateUntil?.ToString("yyyy-MM-dd") ?? "Brak";
                TxtDetModules.Text = string.IsNullOrEmpty(lic.ModulesJson) ? "Brak modułów" : lic.ModulesJson.Replace(", ", "\n• ");
                ModalLicenseDetails.Visibility = Visibility.Visible;
            }
        }
    }

    public void BtnCloseDetModal_Click(object sender, RoutedEventArgs e) => ModalLicenseDetails.Visibility = Visibility.Collapsed;

    public void BtnExportLicense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            using var db = new GeneratorDbContext();
            var lic = db.Licenses.Include(l => l.Company).FirstOrDefault(l => l.Id == id);
            if (lic != null) {
                var sfd = new SaveFileDialog { Filter = "Plik licencji|*.lic", FileName = $"DAPNET_{lic.Company.Nip}_{lic.ExpiresAt:yyyyMMdd}.lic" };
                if (sfd.ShowDialog() == true) File.WriteAllText(sfd.FileName, lic.LicenseBlob);
            }
        }
    }

    public void BtnDeleteLicense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            if (CustomMessageBox.Show("Usunąć licencję z historii?", "Potwierdzenie", CustomMessageBox.MessageBoxType.Question) == CustomMessageBox.MessageBoxResult.Yes)
            {
                using var db = new GeneratorDbContext();
                var lic = db.Licenses.Find(id);
                if (lic != null) { var cid = lic.CompanyId; db.Licenses.Remove(lic); db.SaveChanges(); RefreshLicenseHistory(cid); }
            }
        }
    }

    public void BtnCancelModal_Click(object sender, RoutedEventArgs e) { ModalAddCompany.Visibility = Visibility.Collapsed; ModalNewLicense.Visibility = Visibility.Collapsed; }

    public void BtnExportDatabase_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog { Filter = "Baza danych SQLite|*.db", FileName = $"generator_backup_{DateTime.Now:yyyyMMdd}.db" };
        if (sfd.ShowDialog() == true)
        {
            try { 
                SqliteConnection.ClearAllPools();
                File.Copy("generator.db", sfd.FileName, true); 
                CustomMessageBox.Show("✅ Baza danych została wyeksportowana."); 
            }
            catch (Exception ex) { CustomMessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", CustomMessageBox.MessageBoxType.Error); }
        }
    }

    public void BtnImportDatabase_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Baza danych SQLite|*.db" };
        if (ofd.ShowDialog() == true)
        {
            if (CustomMessageBox.Show("Czy na pewno chcesz zastąpić obecną bazę danych? Wszystkie aktualne dane zostaną utracone.", "Ostrzeżenie", CustomMessageBox.MessageBoxType.Question) == CustomMessageBox.MessageBoxResult.Yes)
            {
                try {
                    SqliteConnection.ClearAllPools();
                    string destPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "generator.db");
                    File.Copy(ofd.FileName, destPath, true);
                    CustomMessageBox.Show("✅ Baza zaimportowana. Program zostanie teraz zamknięty.");
                    Application.Current.Shutdown();
                }
                catch (Exception ex) { CustomMessageBox.Show($"Błąd importu: {ex.Message}", "Błąd", CustomMessageBox.MessageBoxType.Error); }
            }
        }
    }

    public void BtnImportPrivateKey_Click(object sender, RoutedEventArgs e) => ImportKey("PrivateKey");
    public void BtnImportPublicKey_Click(object sender, RoutedEventArgs e) => ImportKey("PublicKey");
    private void ImportKey(string type) {
        var ofd = new OpenFileDialog { Filter = "Pliki XML|*.xml" };
        if (ofd.ShowDialog() == true) {
            using var db = new GeneratorDbContext();
            var s = db.Settings.FirstOrDefault(x => x.Key == type);
            if (s == null) db.Settings.Add(new AppSettings { Key = type, Value = File.ReadAllText(ofd.FileName) });
            else s.Value = File.ReadAllText(ofd.FileName);
            db.SaveChanges(); CheckKeys();
        }
    }
    public void BtnDeletePrivKey_Click(object sender, RoutedEventArgs e) => DeleteKey("PrivateKey");
    public void BtnDeletePubKey_Click(object sender, RoutedEventArgs e) => DeleteKey("PublicKey");
    private void DeleteKey(string key) {
        if (CustomMessageBox.Show($"Usunąć {key}?", "Potwierdzenie", CustomMessageBox.MessageBoxType.Question) == CustomMessageBox.MessageBoxResult.Yes) {
            using var db = new GeneratorDbContext();
            var s = db.Settings.FirstOrDefault(x => x.Key == key);
            if (s != null) { db.Settings.Remove(s); db.SaveChanges(); CheckKeys(); }
        }
    }
    public void BtnKeyGen_Click(object sender, RoutedEventArgs e) {
        if (CustomMessageBox.Show("Wygenerować nowe klucze?", "Ostrzeżenie", CustomMessageBox.MessageBoxType.Question) == CustomMessageBox.MessageBoxResult.Yes) {
            using var rsa = new RSACryptoServiceProvider(2048);
            using var db = new GeneratorDbContext();
            var s1 = db.Settings.FirstOrDefault(x => x.Key == "PrivateKey");
            if (s1 == null) db.Settings.Add(new AppSettings { Key = "PrivateKey", Value = rsa.ToXmlString(true) }); else s1.Value = rsa.ToXmlString(true);
            var s2 = db.Settings.FirstOrDefault(x => x.Key == "PublicKey");
            if (s2 == null) db.Settings.Add(new AppSettings { Key = "PublicKey", Value = rsa.ToXmlString(false) }); else s2.Value = rsa.ToXmlString(false);
            db.SaveChanges(); CheckKeys();
        }
    }
    private void UpdateSetting(GeneratorDbContext db, string key, string value) {
        var s = db.Settings.FirstOrDefault(x => x.Key == key);
        if (s == null) db.Settings.Add(new AppSettings { Key = key, Value = value });
        else s.Value = value;
    }
    public void BtnAddModule_Click(object sender, RoutedEventArgs e) {
        if (string.IsNullOrEmpty(TxtNewModuleCode.Text)) return;
        using var db = new GeneratorDbContext();
        db.Modules.Add(new ModuleDef { Code = TxtNewModuleCode.Text.ToUpper(), Name = TxtNewModuleName.Text });
        db.SaveChanges(); TxtNewModuleCode.Clear(); TxtNewModuleName.Clear(); LoadData();
    }
    public void BtnDeleteModule_Click(object sender, RoutedEventArgs e) {
        if (sender is Button btn && btn.Tag is string code) {
            if (CustomMessageBox.Show($"Usunąć moduł {code}?", "Potwierdzenie", CustomMessageBox.MessageBoxType.Question) == CustomMessageBox.MessageBoxResult.Yes) {
                using var db = new GeneratorDbContext();
                var m = db.Modules.Find(code);
                if (m != null) { db.Modules.Remove(m); db.SaveChanges(); LoadData(); }
            }
        }
    }
}
