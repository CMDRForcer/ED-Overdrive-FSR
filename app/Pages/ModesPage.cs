using System.IO;
using System.Windows;
using System.Windows.Controls;
using GenFrame.Setup.Services;

namespace GenFrame.Setup.Pages;

static class ModesPage
{
    public static UIElement Build(MainWindow window)
    {
        var page = new StackPanel();
        page.Children.Add(Ui.Heading("Schnellwahl"));

        if (window.Game is not { } game) {
            page.Children.Add(Ui.Muted("Zuerst unter „Übersicht“ den Spielordner festlegen."));
            return page;
        }
        if (!File.Exists(Path.Combine(game.Folder, "ReShade.ini"))) {
            page.Children.Add(Ui.Muted("EDAA ist noch nicht eingerichtet. Bitte zuerst unter „Übersicht“ installieren."));
            return page;
        }

        page.Children.Add(Ui.Muted("Ein Klick setzt alle nötigen Werte, Filter und Feineinstellungen bleiben. " +
            "Gilt ab dem nächsten Spielstart, Elite muss dafür geschlossen sein."));

        page.Children.Add(SetupSection(window, game));

        var modesTitle = Ui.Text("EDAA-Modus", Ui.Brush("AccentBrush"));
        modesTitle.Margin = new Thickness(0, 16, 0, 4);
        page.Children.Add(modesTitle);

        var current = AddonSettings.CurrentMode(game);
        var cards = new WrapPanel();
        cards.Children.Add(Card(window, game, QuickMode.DlaaFg, "DLAA + FG", "Beste Bildqualität mit verdoppelter Bildrate. RTX 40/50.", current));
        cards.Children.Add(Card(window, game, QuickMode.Dlaa, "Nur DLAA", "Kantenglättung von NVIDIA, ohne generierte Bilder.", current));
        cards.Children.Add(Card(window, game, QuickMode.TaaFg, "Nur FG", "Kantenglättung mit TAA statt DLAA. Noch nicht im Spiel geprüft.", current));
        cards.Children.Add(Card(window, game, QuickMode.Fsr, "AMD FSR (experimentell)", "FSR 4 auf Radeon RX 6000 und neuer, sonst FSR 3.1. Ohne FG.", current));
        cards.Children.Add(Card(window, game, QuickMode.Off, "Aus", "Keine Kantenglättung und kein FG. Die ReShade-Filter bleiben.", current));
        page.Children.Add(cards);

        if (current == null) {
            page.Children.Add(Ui.Muted("Die aktuellen Werte passen zu keiner der vier Varianten, sie wurden im Overlay oder von Hand geändert."));
        }

        var renodx = InstallInfo.Scan(game.Folder).Any(c => c.Name == "RenoDX" && c.State == ComponentState.Installed);
        if (renodx) {
            page.Children.Add(Ui.Row("RenoDX ist an", Ui.Text("mit FG schwarzes Bild, im Overlay unter „Add-ons“ abschalten", Ui.Brush("WarnBrush"))));
        }
        return page;
    }

    // Render scale, DLSS 5, EDAA's method, graphics and filters together (GameSetups).
    static StackPanel SetupSection(MainWindow window, GameInstall game)
    {
        var section = new StackPanel();
        var title = Ui.Text("Spiel-Setup", Ui.Brush("AccentBrush"));
        title.Margin = new Thickness(0, 12, 0, 4);
        section.Children.Add(title);

        var current = GameSetups.Current(game);
        var dlss5 = GameSetups.Dlss5Installed(game);
        var cards = new WrapPanel();
        cards.Children.Add(SetupCard(window, game, GameSetup.Supersampling, "Supersampling 1,25",
            "Elite rendert größer, DLAA glättet. Ruhiges Bild mit Ultra-Grafik (Lampenschatten High, ohne Tiefenunschärfe). " +
            "Gemessen auf einer RTX 4070 Ti: 70 echte → 140 Bilder/s, im vollen Concourse etwa 65.", current, true));
        cards.Children.Add(SetupCard(window, game, GameSetup.Dlss5, "DLSS 5",
            dlss5 ? "Elite rendert mit 0,85, DLSS 5 verbessert das Bild, DLSS skaliert hoch. Grafik höher. " +
                "Gemessen auf einer RTX 4070 Ti: etwa 67 echte → 135 Bilder/s."
                : "Zuerst auf der Seite „DLSS 5“ installieren.", current, dlss5));
        section.Children.Add(cards);
        section.Children.Add(Ui.Muted(current == null
            ? "Die aktuellen Werte passen zu keinem der beiden Setups. Ein Klick stellt Auflösung, DLSS 5, Kantenglättung, Grafik und Filter passend ein, mit Backup."
            : "Ein Klick stellt Auflösung, DLSS 5, Kantenglättung, Grafik und Filter passend ein, mit Backup. Feineinstellungen der Filter bleiben."));
        return section;
    }

    static Border SetupCard(MainWindow window, GameInstall game, GameSetup setup, string title, string text, GameSetup? current, bool available)
    {
        var content = new StackPanel();
        content.Children.Add(Ui.Text(title));
        content.Children.Add(Ui.Muted(text, 8));
        if (setup == current) {
            content.Children.Add(Ui.Text("aktiv", Ui.Brush("OkBrush")));
        }
        else if (available) {
            var use = Ui.Button("Verwenden", (_, _) => {
                try {
                    GameSetups.Apply(game, setup, _ => { });
                    window.Refresh();
                }
                catch (Exception e) {
                    Ui.Error(e);
                }
            });
            use.HorizontalAlignment = HorizontalAlignment.Left;
            content.Children.Add(use);
        }
        var card = Ui.Card(content, 320);
        if (setup == current) {
            card.BorderBrush = Ui.Brush("AccentBrush");
        }
        return card;
    }

    static Border Card(MainWindow window, GameInstall game, QuickMode mode, string title, string text, QuickMode? current)
    {
        var content = new StackPanel();
        content.Children.Add(Ui.Text(title));
        content.Children.Add(Ui.Muted(text, 8));
        if (mode == current) {
            content.Children.Add(Ui.Text("aktiv", Ui.Brush("OkBrush")));
        }
        else {
            var use = Ui.Button("Verwenden", (_, _) => {
                try {
                    if (GameState.IsRunning()) {
                        throw new InvalidOperationException("Elite läuft. Bitte das Spiel beenden und erneut versuchen.");
                    }
                    BackupService.Create("Vor Schnellwahl", BackupsPage.FilesToSave(game));
                    AddonSettings.Write(game, AddonSettings.ModeValues(mode));
                    window.Refresh();
                }
                catch (Exception e) {
                    Ui.Error(e);
                }
            });
            use.HorizontalAlignment = HorizontalAlignment.Left;
            content.Children.Add(use);
        }
        var card = Ui.Card(content, 210);
        if (mode == current) {
            card.BorderBrush = Ui.Brush("AccentBrush");
        }
        return card;
    }
}
