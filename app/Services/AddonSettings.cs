using System.Globalization;
using System.IO;

namespace GenFrame.Setup.Services;

public enum QuickMode { DlaaFg, Dlaa, TaaFg, Off, Fsr, FsrFg }

public enum SettingKind { Toggle, Whole, Real, Choice }

public sealed record AddonSetting(string Group, string Key, string Label, string Hint, SettingKind Kind, string Default,
    double Min = 0, double Max = 1, (string Value, string Text)[]? Choices = null);

// The add-on's [EDAA] values in ReShade.ini. ReShade rewrites the file while Elite runs, so these
// are only changed while the game is closed; the add-on reads them at the next start.
public static class AddonSettings
{
    public const string Section = "EDAA";

    public static readonly (string Value, string Text)[] Methods = [
        ("0", "EDAA TAA"), ("1", "NVIDIA DLSS"), ("2", "Intel XeSS"), ("3", "AMD FSR 4 / 3.1 (experimentell)"),
    ];

    public static readonly (string Value, string Text)[] FgBackends = [
        ("0", "NVIDIA DLSS-G (RTX 40/50)"), ("1", "AMD FSR Frame Generation (experimentell, jede Karte)"),
    ];

    public static readonly (string Value, string Text)[] DlaaModels = [
        ("0", "NVIDIA-Standard"), ("10", "J"), ("11", "K"), ("12", "L"), ("13", "M"),
    ];

    // The values worth changing. Defaults as in docs/plan.md, ranges as the overlay's sliders.
    public static readonly AddonSetting[] All = [
        new("Frame Generation", "fg_backend", "Verfahren", "Wird beim Spielstart gelesen. AMD FSR FG ist experimentell und nutzt EDAAs eigenes Bildlimit statt Reflex.", SettingKind.Choice, "0", Choices: FgBackends),
        new("Frame Generation", "fg_hud_protect", "HUD ruhig halten", "Das Cockpit-HUD verschmiert nicht in generierten Bildern.", SettingKind.Toggle, "1"),
        new("Frame Generation", "fg_fps_limit", "Bildlimit (angezeigte FPS, 0 = aus)", "Knapp unter der Bildwiederholrate, zum Beispiel 140 bei 144 Hz. Keinen zweiten Limiter nutzen.", SettingKind.Whole, "140", 0, 360),
        new("Kantenglättung", "aa_method", "Methode", "AMD FSR ist experimentell: FSR 4 auf Radeon RX 6000 und neuer, sonst FSR 3.1.", SettingKind.Choice, "1", Choices: Methods),
        new("Kantenglättung", "fsr_sharpness", "FSR-Schärfe (0 = aus)", "Nur für AMD FSR.", SettingKind.Real, "0", 0, 1),
        new("Kantenglättung", "dlss_preset", "DLAA-Modell", "Das Modell, das EDAA bei NVIDIA anfordert.", SettingKind.Choice, "11", Choices: DlaaModels),
        new("Kantenglättung", "taa_before_hud", "Vor dem HUD glätten", "An: FG-HUD-Schutz voll, das HUD bleibt aber ungeglättet. Aus: HUD geglättet, FG-HUD-Schutz schwächer.", SettingKind.Toggle, "1"),
        new("Kantenglättung", "hud_learn", "Cockpit-HUD an den Panel-Texturen erkennen", "Nötig, wenn EDHM die HUD-Shader ersetzt: sonst wirken HUD-Maske und FG-HUD-Schutz nicht.", SettingKind.Toggle, "1"),
        new("Kantenglättung", "hud_motion", "Cockpit-HUD bewegt sich mit dem Cockpit", "Panels bleiben bei Kopf- und Schiffsbewegung scharf. Wirkt nur beim Hochskalieren (unter 1,0).", SettingKind.Toggle, "1"),
        new("Hochskalieren (nur unter 1,0 Supersampling)", "game_upscale", "Hochskalieren dem Spiel überlassen", "", SettingKind.Toggle, "0"),
        new("Hochskalieren (nur unter 1,0 Supersampling)", "upscale_sharpness", "Schärfe bei TAA-Hochskalierung", "", SettingKind.Real, "0.5", 0, 1),
        new("Hochskalieren (nur unter 1,0 Supersampling)", "upscale_hud_mask", "HUD ohne Nachzieher", "", SettingKind.Toggle, "1"),
        new("Hochskalieren (nur unter 1,0 Supersampling)", "upscale_mip_bias", "Texturen in Ausgabeauflösung", "", SettingKind.Toggle, "1"),
        new("TAA", "taa_history_weight", "Anteil der Vorbilder", "Mehr ist ruhiger, aber unschärfer.", SettingKind.Real, "0.93", 0.5, 0.98),
        new("TAA", "taa_history_weight_motion", "Anteil der Vorbilder bei Bewegung", "Weniger gibt weniger Nachzieher, aber mehr Treppenstufen.", SettingKind.Real, "0.9", 0.3, 0.95),
        new("TAA", "taa_history_weight_min", "Mindestanteil gegen Flimmern", "", SettingKind.Real, "0.95", 0.5, 0.95),
        new("TAA", "taa_clip_gamma", "Clip-Box bei Bewegung", "", SettingKind.Real, "1.25", 0.5, 3),
        new("TAA", "taa_clip_gamma_static", "Clip-Box im Stillstand", "Größer flimmert weniger.", SettingKind.Real, "5", 0.5, 5),
        new("TAA", "taa_anti_flicker", "Flimmerschutz", "", SettingKind.Toggle, "1"),
        new("TAA", "taa_soft_offscreen", "Weicher Bildrand", "", SettingKind.Toggle, "1"),
        new("TAA", "taa_reconstruction", "Weichere Rekonstruktion", "", SettingKind.Toggle, "0"),
        new("TAA", "taa_unjitter", "Ohne Jitter abtasten", "Weicheres Bild.", SettingKind.Toggle, "0"),
        new("Korrekturen fürs Spiel", "fix_sun_shadows", "Sonnenschatten korrigieren", "", SettingKind.Toggle, "1"),
        new("Korrekturen fürs Spiel", "vehicle_motion", "Bewegung des eigenen Fahrzeugs", "", SettingKind.Toggle, "1"),
        new("Korrekturen fürs Spiel", "jitter_overlay_draws", "Decals und Lichter mitbewegen", "Sonst flimmern sie.", SettingKind.Toggle, "1"),
        new("Korrekturen fürs Spiel", "map_detect_no_shadows", "Karten an fehlenden Schatten erkennen", "", SettingKind.Toggle, "1"),
    ];

    static string IniPath(GameInstall game) => Path.Combine(game.Folder, "ReShade.ini");

    public static string Read(GameInstall game, AddonSetting setting) =>
        Get(game, setting.Key) ?? setting.Default;

    public static string? Get(GameInstall game, string key) =>
        File.Exists(IniPath(game)) ? IniFile.Load(IniPath(game)).Get(Section, key) : null;

    public static void Write(GameInstall game, IEnumerable<KeyValuePair<string, string>> values)
    {
        if (!File.Exists(IniPath(game))) {
            throw new FileNotFoundException("ReShade.ini fehlt. Bitte zuerst unter „Übersicht“ installieren.");
        }
        var ini = IniFile.Load(IniPath(game));
        foreach (var (key, value) in values) {
            ini.Set(Section, key, value);
        }
        ini.Save(IniPath(game));
    }

    public static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // Null when the values match none of the four modes (changed by hand or in the overlay).
    public static QuickMode? CurrentMode(GameInstall game)
    {
        bool On(string key) => Get(game, key) == "1";
        var method = Get(game, "aa_method");
        if (!On("taa_enabled")) {
            return !On("fg_generate") ? QuickMode.Off : null;
        }
        return (method, On("fg_generate")) switch {
            ("1", true) => QuickMode.DlaaFg,
            ("1", false) => QuickMode.Dlaa,
            ("0", true) => QuickMode.TaaFg,
            ("3", false) => QuickMode.Fsr,
            ("3", true) => QuickMode.FsrFg,
            _ => null,
        };
    }

    // docs/plan.md, "Schnellwahl". Filters and the other settings stay as they are.
    public static Dictionary<string, string> ModeValues(QuickMode mode)
    {
        var values = new Dictionary<string, string>();
        var antiAliasing = mode != QuickMode.Off;
        var frameGeneration = mode is QuickMode.DlaaFg or QuickMode.TaaFg or QuickMode.FsrFg;

        values["taa_enabled"] = antiAliasing ? "1" : "0";
        values["jitter_enabled"] = antiAliasing ? "1" : "0"; // TAA needs jitter, the plain image does not
        if (antiAliasing) {
            values["aa_method"] = mode == QuickMode.TaaFg ? "0" : mode is QuickMode.Fsr or QuickMode.FsrFg ? "3" : "1";
        }

        values["fg_generate"] = frameGeneration ? "1" : "0";
        values["fg_present_test"] = frameGeneration ? "1" : "0";
        if (frameGeneration) {
            values["fg_backend"] = mode == QuickMode.FsrFg ? "1" : "0";
            values["fg_factory"] = "1";
            values["motion_enabled"] = "1";
            values["fg_hud_protect"] = "1";
            values["taa_before_hud"] = "1";
            values["hud_learn"] = "1";
            values["hud_motion"] = "1";
        }
        return values;
    }
}
