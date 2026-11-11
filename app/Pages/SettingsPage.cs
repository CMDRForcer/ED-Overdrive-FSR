using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GenFrame.Setup.Services;

namespace GenFrame.Setup.Pages;

static class SettingsPage
{
    public static UIElement Build(MainWindow window)
    {
        var page = new StackPanel();
        page.Children.Add(Ui.Heading("Einstellungen"));

        if (window.Game is not { } game) {
            page.Children.Add(Ui.Muted("Zuerst unter „Übersicht“ den Spielordner festlegen."));
            return page;
        }
        if (!File.Exists(Path.Combine(game.Folder, "ReShade.ini"))) {
            page.Children.Add(Ui.Muted("EDAA ist noch nicht eingerichtet. Bitte zuerst unter „Übersicht“ installieren."));
            return page;
        }

        page.Children.Add(Ui.Muted("Gilt ab dem nächsten Spielstart. Geändert wird erst beim Speichern, bei geschlossenem Elite und mit Backup vorher. " +
            "Kantenglättung und FG an oder aus stehen unter „Schnellwahl“."));

        var changes = new Dictionary<string, string>();
        foreach (var group in AddonSettings.Shown.GroupBy(s => s.Group)) {
            var title = Ui.Text(group.Key, Ui.Brush("AccentBrush"));
            title.Margin = new Thickness(0, 12, 0, 2);
            page.Children.Add(title);
            foreach (var setting in group) {
                page.Children.Add(Row(game, setting, changes));
            }
        }

        page.Children.Add(Ui.Buttons(
            Ui.Button("Speichern", (_, _) => Save(window, game, "Vor Einstellungen", changes), primary: true),
            Ui.Button("Verwerfen", (_, _) => window.Refresh()),
            Ui.Button("Auf Standard", (_, _) => {
                var question = "Alle Werte dieser Seite auf Standard setzen? Die Schnellwahl bleibt, vorher wird ein Backup angelegt.";
                if (MessageBox.Show(window, question, "ED-Overdrive", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) {
                    Save(window, game, "Vor Standardwerten", AddonSettings.Defaults());
                }
            })));
        return page;
    }

    static void Save(MainWindow window, GameInstall game, string label, Dictionary<string, string> values)
    {
        try {
            if (GameState.IsRunning()) {
                throw new InvalidOperationException("Elite läuft. Bitte das Spiel beenden und erneut versuchen.");
            }
            if (values.Count > 0) {
                BackupService.Create(label, BackupsPage.FilesToSave(game));
                AddonSettings.Write(game, values);
            }
            window.Refresh();
        }
        catch (Exception e) {
            Ui.Error(e);
        }
    }

    static Border Row(GameInstall game, AddonSetting setting, Dictionary<string, string> changes)
    {
        var left = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(Ui.Text(setting.Label));
        if (setting.Hint.Length > 0) {
            var hint = Ui.Muted(setting.Hint, 0);
            hint.TextWrapping = TextWrapping.Wrap;
            left.Children.Add(hint);
        }
        // A linked switch is on only when all its keys are.
        var value = setting.Linked is null ? AddonSettings.Read(game, setting)
            : setting.Keys.All(k => (AddonSettings.Get(game, k) ?? setting.Default) != "0") ? "1" : "0";
        return Ui.Row(left, Control(setting, value, changes));
    }

    static FrameworkElement Control(AddonSetting setting, string value, Dictionary<string, string> changes)
    {
        switch (setting.Kind) {
            case SettingKind.Toggle: {
                var box = new CheckBox { IsChecked = value != "0", VerticalAlignment = VerticalAlignment.Center };
                box.Click += (_, _) => {
                    foreach (var key in setting.Keys) {
                        changes[key] = box.IsChecked == true ? "1" : "0";
                    }
                };
                return box;
            }
            case SettingKind.Choice: {
                var box = new ComboBox { Width = 170, VerticalAlignment = VerticalAlignment.Center };
                var choices = setting.Choices!.ToList();
                if (choices.All(c => c.Value != value)) {
                    choices.Add((value, $"Wert {value}")); // set by hand: shown, not changed
                }
                foreach (var choice in choices) {
                    box.Items.Add(choice.Text);
                }
                box.SelectedIndex = choices.FindIndex(c => c.Value == value);
                box.SelectionChanged += (_, _) => changes[setting.Key] = choices[box.SelectedIndex].Value;
                return box;
            }
            default: {
                var whole = setting.Kind == SettingKind.Whole;
                var number = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed : double.Parse(setting.Default, CultureInfo.InvariantCulture);
                var slider = new Slider {
                    Width = 170,
                    // A value outside the overlay's range stays as it is until the user moves the slider.
                    Minimum = Math.Min(setting.Min, number),
                    Maximum = Math.Max(setting.Max, number),
                    Value = number,
                    TickFrequency = whole ? 1 : 0.01,
                    IsSnapToTickEnabled = true,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var shown = Ui.Text(AddonSettings.Format(number));
                shown.Width = 44;
                shown.TextAlignment = TextAlignment.Right;
                shown.VerticalAlignment = VerticalAlignment.Center;
                slider.ValueChanged += (_, e) => {
                    var text = AddonSettings.Format(e.NewValue);
                    shown.Text = text;
                    changes[setting.Key] = text;
                };
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add(slider);
                panel.Children.Add(shown);
                return panel;
            }
        }
    }
}
