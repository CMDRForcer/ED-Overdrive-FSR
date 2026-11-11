using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GenFrame.Setup.Pages;
using GenFrame.Setup.Services;

namespace GenFrame.Setup;

public partial class MainWindow : Window
{
    readonly DispatcherTimer gameStateTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    string currentPage = "overview";

    public MainWindow()
    {
        InitializeComponent();
        Title = $"ED-Overdrive für {Vendor.Name} – FSR experimentell";
        // AMD build: DLSS 5 and the NVIDIA driver presets run on NVIDIA cards only (edaa/docs/vendors.md).
        foreach (var item in Navigation.Children.OfType<RadioButton>().Where(b => Vendor.Amd && b.Tag is "dlss5" or "presets").ToList()) {
            Navigation.Children.Remove(item);
        }
        UpdateGameState();
        gameStateTimer.Tick += (_, _) => UpdateGameState();
        gameStateTimer.Start();
        ShowPage(currentPage);
    }

    public GameInstall? Game { get; private set; } = AppSettings.LoadGame();

    public void SetGame(GameInstall game)
    {
        Game = game;
        AppSettings.SaveGame(game);
        ShowPage(currentPage);
    }

    public void Refresh() => ShowPage(currentPage);

    void OnNavigate(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string page } && PageHost != null) {
            ShowPage(page);
        }
    }

    void ShowPage(string page)
    {
        currentPage = page;
        PageHost.Content = page switch {
            "overview" => OverviewPage.Build(this),
            "backups" => BackupsPage.Build(this),
            "modes" => ModesPage.Build(this),
            "filters" => FilterPage.Build(this),
            "graphics" => GraphicsPage.Build(this),
            "dlss5" => Dlss5Page.Build(this),
            "check" => CheckPage.Build(this),
            "presets" => PresetsPage.Build(this),
            "settings" => SettingsPage.Build(this),
            "diagnose" => DiagnosePage.Build(this),
            _ => null,
        };
    }

    void UpdateGameState()
    {
        bool running = GameState.IsRunning();
        GameStateText.Text = running ? "Elite läuft – Änderungen gesperrt" : "Elite ist geschlossen";
        GameStateText.Foreground = (Brush)FindResource(running ? "WarnBrush" : "OkBrush");
        GameStateBadge.Background = (Brush)FindResource("CardBrush");
    }
}
