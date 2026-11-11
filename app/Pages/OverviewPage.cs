using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using GenFrame.Setup.Services;

namespace GenFrame.Setup.Pages;

static class OverviewPage
{
    public static UIElement Build(MainWindow window)
    {
        var page = new StackPanel();
        page.Children.Add(Ui.Heading("Übersicht"));

        var game = window.Game;
        if (game == null) {
            page.Children.Add(Ui.Muted("Elite Dangerous Odyssey wurde nicht automatisch gefunden (Steam, Epic, Frontier)."));
            page.Children.Add(Ui.Buttons(Ui.Button("Spielordner wählen", (_, _) => ChooseFolder(window), primary: true)));
            return page;
        }

        page.Children.Add(Ui.Muted($"Elite Dangerous Odyssey · {game.Store} · {game.Folder}"));

        var cards = new WrapPanel();
        var components = InstallInfo.Scan(game.Folder);
        foreach (var c in components.Where(c => !c.Foreign)) {
            var content = new StackPanel();
            content.Children.Add(Ui.Muted(c.Name, 2));
            content.Children.Add(Ui.Text(c.State == ComponentState.Installed ? c.Detail : "fehlt",
                Ui.Brush(c.State == ComponentState.Installed ? "TextBrush" : "WarnBrush")));
            cards.Children.Add(Ui.Card(content, 190));
        }
        page.Children.Add(cards);

        foreach (var c in components.Where(c => c.Foreign)) {
            var state = c.State == ComponentState.Disabled ? "erkannt (deaktiviert)" : "erkannt";
            page.Children.Add(Ui.Row($"{c.Name} {state}", Ui.Text(c.Detail, Ui.Brush("MutedBrush"))));
        }

        // RenoDX switches the game's output to HDR, but with frame generation EDAA's D3D12 output (SDR only
        // until Phase 3) shows the picture: black screen on 03.11.2026.
        var renodxOn = components.Any(c => c.Name == "RenoDX" && c.State == ComponentState.Installed);
        var iniPath = System.IO.Path.Combine(game.Folder, "ReShade.ini");
        var d3d12Output = System.IO.File.Exists(iniPath) && IniFile.Load(iniPath).Get("EDAA", "fg_present_test") == "1";
        if (renodxOn && d3d12Output) {
            page.Children.Add(Ui.Row("RenoDX ist an, zusammen mit Frame Generation",
                Ui.Text("schwarzes Bild – RenoDX im Overlay unter „Add-ons“ abschalten", Ui.Brush("WarnBrush"))));
        }

        if (InstallInfo.LoadedVersion(game.Folder) is { } loaded) {
            page.Children.Add(Ui.Row("Zuletzt im Spiel geladen", Ui.Text(loaded, Ui.Brush("MutedBrush"))));
        }

        var session = GameState.LastSession();
        if (session is { } s) {
            var end = s.CleanShutdown ? "sauber beendet" : "ohne Shutdown-Eintrag (Absturz?)";
            page.Children.Add(Ui.Row($"Letzte Sitzung: {s.Start:dd.MM. HH:mm}–{s.End:HH:mm}", Ui.Text(end,
                Ui.Brush(s.CleanShutdown ? "OkBrush" : "WarnBrush"))));
        }

        var channel = new ComboBox { Width = 230, Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        channel.Items.Add($"Stabil (EDAA {InstallService.BundledEdaa}, in der App)");
        // Experimental FSR build: no development channel, it would install the public builds/main without FSR.
        channel.SelectedIndex = 0;
        channel.IsEnabled = false;
        page.Children.Add(channel);
        Channel Selected() => Channel.Stable;

        page.Children.Add(Ui.Buttons(
            Ui.Button("Installieren / Aktualisieren", (_, _) => InstallDialog.Run(window, game, Selected(), clean: false), primary: true),
            Ui.Button("Sauber neu installieren", (_, _) => InstallDialog.Run(window, game, Selected(), clean: true)),
            Ui.Button("Anderen Spielordner wählen", (_, _) => ChooseFolder(window))));
        return page;
    }

    static void ChooseFolder(MainWindow window)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit EliteDangerous64.exe wählen" };
        if (dialog.ShowDialog(window) != true) {
            return;
        }
        if (!GameLocator.IsGameFolder(dialog.FolderName)) {
            MessageBox.Show(window, "In diesem Ordner liegt keine EliteDangerous64.exe.", "ED-Overdrive");
            return;
        }
        window.SetGame(new GameInstall(dialog.FolderName, GameStore.Manual));
    }
}
