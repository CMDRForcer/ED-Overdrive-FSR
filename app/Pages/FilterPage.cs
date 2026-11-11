using System.IO;
using System.Windows;
using System.Windows.Controls;
using GenFrame.Setup.Services;

namespace GenFrame.Setup.Pages;

static class FilterPage
{
    public static UIElement Build(MainWindow window)
    {
        var page = new StackPanel();
        page.Children.Add(Ui.Heading("Filter"));
        page.Children.Add(Ui.Muted("SDR-Filter-Set in drei Varianten. Zum Ausprobieren im Overlay (Pos1) liegen außerdem " +
            "AO (MXAO), Fake-HDR, Bloom, Tiefenunschärfe und Film Grain bereit – standardmäßig aus, weil sie Frame Generation " +
            "oder das HUD stören können. AO und Tiefenunschärfe nur ohne Frame Generation."));

        if (window.Game is not { } game) {
            page.Children.Add(Ui.Muted("Zuerst unter „Übersicht“ den Spielordner festlegen."));
            return page;
        }

        var active = FilterService.Active(game);
        // The DLSS 5 variant came later; SetActive writes its preset when it is missing.
        var installed = Enum.GetValues<FilterVariant>().Where(v => v != FilterVariant.Dlss5)
            .All(v => File.Exists(Path.Combine(game.Folder, FilterService.PresetFile(v))));

        // The black level fix follows Windows HDR in every variant (FilterService.SyncBlackLevelFix).
        var hdr = DisplayInfo.HdrEnabled();
        if (active is { } shown && hdr is { } on) {
            var fixOn = FilterService.BlackLevelFixOn(Path.Combine(game.Folder, FilterService.PresetFile(shown)));
            var text = $"Windows-HDR {(on ? "an" : "aus")} · Schwarzwert-Fix {(fixOn ? "an" : "aus")}";
            if (fixOn == on) {
                page.Children.Add(Ui.Row(text, Ui.Text("passt", Ui.Brush("OkBrush"))));
            }
            else {
                var sync = Ui.Button("Anpassen", (_, _) => {
                    try {
                        if (GameState.IsRunning()) {
                            throw new InvalidOperationException("Elite läuft. Bitte das Spiel beenden und erneut versuchen.");
                        }
                        new FilterService(game, _ => { }).SyncBlackLevelFix();
                        window.Refresh();
                    }
                    catch (Exception e) {
                        Ui.Error(e);
                    }
                });
                page.Children.Add(Ui.Row(text, sync));
            }
        }

        var cards = new WrapPanel();
        // AMD build: no DLSS 5 (edaa/docs/vendors.md).
        foreach (var variant in Enum.GetValues<FilterVariant>().Where(v => Vendor.Nvidia || v != FilterVariant.Dlss5)) {
            var content = new StackPanel();
            content.Children.Add(Ui.Text(Name(variant)));
            content.Children.Add(Ui.Muted(FilterService.Describe(variant), 8));
            if (variant == active) {
                content.Children.Add(Ui.Text("aktiv", Ui.Brush("OkBrush")));
            }
            else if (installed) {
                var use = Ui.Button("Verwenden", (_, _) => {
                    try {
                        if (GameState.IsRunning()) {
                            throw new InvalidOperationException("Elite läuft. Bitte das Spiel beenden und erneut versuchen.");
                        }
                        new FilterService(game, _ => { }).SetActive(variant);
                        window.Refresh();
                    }
                    catch (Exception e) {
                        Ui.Error(e);
                    }
                });
                use.HorizontalAlignment = HorizontalAlignment.Left;
                content.Children.Add(use);
            }
            var card = Ui.Card(content, 175);
            if (variant == active) {
                card.BorderBrush = Ui.Brush("AccentBrush");
            }
            cards.Children.Add(card);
        }
        page.Children.Add(cards);

        page.Children.Add(Ui.Muted("Reihenfolge: Deband → Cinetools LUT 40 % → LocalContrastCS 0.35 → Vibrance 0.12 → CAS 0.40 → " +
            "Schwarzwert-Fix (Gamma 2.2 → sRGB, nur mit Windows-HDR). " +
            "Feinjustieren im ReShade-Overlay (Pos1). Varianten hier umschalten, bei geschlossenem Spiel.", 4));
        page.Children.Add(Ui.Muted("Quellen: ReShade-Standard, prod80, Insane-Shaders, SweetFX, Lilium (GPL-3), qUINT (Marty McFly) – " +
            "geladen von den Original-Adressen, nicht mitgeliefert."));

        var buttons = new List<Button> { Ui.Button(installed ? "Shader aktualisieren" : "Filter-Set installieren", (_, _) => {
            var variant = active ?? FilterVariant.Vivid;
            InstallDialog.RunTask(window, "Filter-Set", async log => {
                BackupService.Create("Vor Filter-Set", BackupsPage.FilesToSave(game));
                log("Backup angelegt.");
                await new FilterService(game, log).Install(variant);
                log("Fertig.");
            });
        }, primary: true) };
        if (active is { } current) {
            buttons.Add(Ui.Button($"„{Name(current)}“ auf Startwerte", (_, _) => {
                InstallDialog.RunTask(window, "Startwerte", log => {
                    BackupService.Create("Vor Startwerten", BackupsPage.FilesToSave(game));
                    log("Backup angelegt.");
                    new FilterService(game, log).RestoreDefaults(current);
                    return Task.CompletedTask;
                });
            }));
        }
        page.Children.Add(Ui.Buttons([.. buttons]));
        return page;
    }

    static string Name(FilterVariant variant) => variant switch {
        FilterVariant.Natural => "Natürlich",
        FilterVariant.Vivid => "Lebendig",
        FilterVariant.Dlss5 => "DLSS 5",
        _ => "Leistung",
    };
}
