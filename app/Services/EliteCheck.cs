using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GenFrame.Setup.Services;

public enum CheckStatus { Ok, Warning, Info }

public sealed record CheckResult(string Group, string Title, CheckStatus Status, string Detail,
    string? FixLabel = null, Action? Fix = null);

// Looks for settings that are known to clash with EDAA. Run() only reads; a fix only runs when the
// user clicks it, and the page makes the backup first. EDHM, RenoDX, the driver and Windows are never changed.
public sealed partial class EliteCheck(GameInstall game, string graphicsFolder)
{
    public const string GroupGraphics = "Grafik im Spiel";
    public const string GroupLimiter = "Limiter und Schärfe";
    public const string GroupFg = "Frame Generation";
    public const string GroupFiles = "Spielordner";

    // Everything else in the game folder that looks like one of these is a leftover of an old test.
    static readonly string[] OldBuildPatterns = [
        "EDAA.addon64?*", "GenFrame.addon64?*", "EDAA?*.addon64", "GenFrame?*.addon64", "nvngx_dlss*.dll?*",
        "renodx-dlss5*", "dlss5-*", "DLSS5*", "RenoDX-DLSS5*",
    ];
    static readonly string[] StreamlineFiles = [
        "nvngx_dlssg.dll", "sl.common.dll", "sl.dlss_g.dll", "sl.interposer.dll", "sl.pcl.dll", "sl.reflex.dll",
    ];

    public static string DefaultGraphicsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Frontier Developments", "Elite Dangerous", "Options", "Graphics");

    string SettingsXml => Path.Combine(graphicsFolder, "Settings.xml");
    string DisplayXml => Path.Combine(graphicsFolder, "DisplaySettings.xml");
    string CustomXml => Path.Combine(graphicsFolder, "Custom.4.4.fxcfg");
    string ReShadeIni => Path.Combine(game.Folder, "ReShade.ini");

    string? Edaa(string key) => File.Exists(ReShadeIni) ? IniFile.Load(ReShadeIni).Get("EDAA", key) : null;

    // The D3D12 output and frame generation are separate keys: the output decides what is on screen.
    bool OutputOn => Edaa("fg_present_test") == "1";
    bool GenerateOn => Edaa("fg_generate") == "1";

    public List<CheckResult> Run()
    {
        var results = new List<CheckResult>();
        CheckGraphics(results);
        CheckLimiters(results);
        CheckFrameGeneration(results);
        CheckFiles(results);
        return results;
    }

    void CheckGraphics(List<CheckResult> results)
    {
        var preset = XmlValue(SettingsXml, "PresetName");
        // Only the preset "Custom" uses Custom.4.4.fxcfg (assumption from the file's name and PresetName).
        var custom = preset == null || preset == "Custom";
        if (!custom) {
            results.Add(new(GroupGraphics, "Grafik-Preset", CheckStatus.Info,
                $"Elite nutzt „{preset}“, Supersampling kann die App dann nicht auslesen."));
        }

        if (custom && Number(XmlValue(CustomXml, "SSAAMultiplier")) is { } ss) {
            var text = ss.ToString("0.0#", CultureInfo.GetCultureInfo("de-DE"));
            if (Math.Abs(ss - 1) < 0.001) {
                results.Add(new(GroupGraphics, "Supersampling", CheckStatus.Ok, $"{text}, passt."));
            }
            else {
                // Below 1x EDAA upscales with whatever method is chosen: DLAA becomes DLSS, XeSS Native AA XeSS (docs/addon.md 6e).
                var method = Edaa("aa_method") ?? "0";
                var upscaler = method switch { "1" => "DLSS", "0" => "EDAA TAA", _ => "XeSS" };
                var dlss5 = File.Exists(Path.Combine(game.Folder, "gitc-uplift.addon64"));
                var setup = Math.Abs(ss - GameSetups.SupersamplingScale) < 0.001 && !UpliftService.Read(game).Enabled;
                var why = setup
                    ? "So gewollt mit dem Spiel-Setup „Supersampling 1,25“. Gemessen auf einer RTX 4070 Ti mit DLAA und Ultra-Grafik: 70 echte → 140 Bilder/s, im vollen Concourse etwa 65."
                    : ss > 1
                    ? "Das kostet viel Leistung. Gemessen auf einer RTX 4070 Ti: Bei 1,0 sind es etwa 68 echte Bilder, bei 1,25 in Stationen etwa 57, bei 2,0 nur noch 3–6."
                    : dlss5 && method == "1" ? "So gewollt mit DLSS 5: Elite rendert kleiner, DLSS 5 rechnet in dieser Größe, DLSS skaliert hoch."
                    : $"Das Spiel rendert kleiner und EDAA skaliert mit {upscaler} hoch.";
                results.Add(new(GroupGraphics, "Supersampling", ss < 1 || setup ? CheckStatus.Info : CheckStatus.Warning,
                    $"{text}. {why}", "Auf 1,0 setzen", () => SetXml(CustomXml, "SSAAMultiplier", "1.000000")));
            }
        }

        var settings = XmlValue(SettingsXml, "TerrainCheckerboardRenderingEnabled");
        var customValue = XmlValue(CustomXml, "TerrainCheckerboardRenderingEnabled");
        if (settings != null || customValue != null) {
            if (settings == "true" || customValue == "true") {
                results.Add(new(GroupGraphics, "Checkerboard-Terrain", CheckStatus.Warning,
                    "An. Das erzeugt flimmernde Linienmuster mit TAA und Frame Generation.", "Ausschalten", () => {
                        if (settings != null) {
                            SetXml(SettingsXml, "TerrainCheckerboardRenderingEnabled", "false");
                        }
                        if (customValue != null) {
                            SetXml(CustomXml, "TerrainCheckerboardRenderingEnabled", "false");
                        }
                    }));
            }
            else {
                results.Add(new(GroupGraphics, "Checkerboard-Terrain", CheckStatus.Ok, "Aus, passt."));
            }
        }
    }

    void CheckLimiters(List<CheckResult> results)
    {
        if (XmlValue(DisplayXml, "LimitFrameRate") == "true") {
            var max = XmlValue(DisplayXml, "MaxFramesPerSecond") ?? "?";
            results.Add(new(GroupLimiter, "Bildlimit im Spiel", GenerateOn ? CheckStatus.Warning : CheckStatus.Info,
                $"An ({max} FPS). Zusammen mit dem Limit von EDAA wird der Takt unruhig.", "Ausschalten",
                () => SetXml(DisplayXml, "LimitFrameRate", "false")));
        }
        else if (File.Exists(DisplayXml)) {
            results.Add(new(GroupLimiter, "Bildlimit im Spiel", CheckStatus.Ok, "Aus, passt."));
        }

        CheckRtss(results);
        CheckFpsLimit(results);
        CheckSharpening(results);

        results.Add(new(GroupLimiter, Vendor.Amd ? "AMD Software (Adrenalin)" : "NVIDIA-Treiber und NVIDIA App", CheckStatus.Info,
            "Schärfe und Bildlimit dort kann die App noch nicht auslesen. Bitte bei Bedarf selbst nachsehen."));
    }

    static void CheckRtss(List<CheckResult> results)
    {
        var folder = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Unwinder\RTSS", "InstallDir", null) as string;
        if (string.IsNullOrEmpty(folder)) {
            return;
        }

        // The game's own profile if there is one, otherwise the global one.
        var profiles = Path.Combine(folder, "Profiles");
        var limit = new[] { Path.Combine(profiles, "EliteDangerous64.exe.cfg"), Path.Combine(profiles, "Global") }
            .Select(RtssLimit).FirstOrDefault(l => l != null);
        var running = System.Diagnostics.Process.GetProcessesByName("RTSS");
        foreach (var p in running) {
            p.Dispose();
        }

        if (limit is > 0) {
            var text = $"{limit:0.##} FPS eingestellt";
            results.Add(running.Length > 0
                ? new(GroupLimiter, "RivaTuner", CheckStatus.Warning, $"{text}. Bitte in RivaTuner auf 0 stellen, zwei Limiter machen den Takt unruhig.")
                : new(GroupLimiter, "RivaTuner", CheckStatus.Info, $"{text}, läuft gerade aber nicht."));
        }
        else {
            results.Add(new(GroupLimiter, "RivaTuner", CheckStatus.Ok,
                running.Length > 0 ? "Läuft, kein Limit gesetzt." : "Installiert, kein Limit gesetzt."));
        }
    }

    static double? RtssLimit(string profile)
    {
        if (!File.Exists(profile)) {
            return null;
        }
        var text = File.ReadAllText(profile);
        var limit = RtssLimitLine().Match(text);
        if (!limit.Success) {
            return null;
        }
        var denominator = RtssDenominatorLine().Match(text);
        var divide = denominator.Success ? double.Parse(denominator.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
        return double.Parse(limit.Groups[1].Value, CultureInfo.InvariantCulture) / Math.Max(divide, 1);
    }

    void CheckFpsLimit(List<CheckResult> results)
    {
        if (!GenerateOn || !int.TryParse(Edaa("fg_fps_limit") ?? "140", out var limit)) {
            return;
        }

        var hz = DisplayInfo.RefreshRate();
        if (hz is not { } rate) {
            results.Add(new(GroupLimiter, "Limit von EDAA", CheckStatus.Info, $"{limit} angezeigte FPS, Bildwiederholrate unbekannt."));
        }
        else if (limit > 0 && limit <= rate) {
            results.Add(new(GroupLimiter, "Limit von EDAA", CheckStatus.Ok, $"{limit} angezeigte FPS bei {rate} Hz, passt."));
        }
        else {
            var target = rate - 4;
            results.Add(new(GroupLimiter, "Limit von EDAA", CheckStatus.Warning,
                (limit == 0 ? "Aus" : $"{limit} angezeigte FPS") + $" bei {rate} Hz. Der Hauptmonitor kann mehr als {rate} Bilder nicht zeigen.",
                $"Auf {target} setzen", () => SetEdaa("fg_fps_limit", target.ToString(CultureInfo.InvariantCulture))));
        }
    }

    void CheckSharpening(List<CheckResult> results)
    {
        var cas = ActivePresetUsesCas();
        if (Number(XmlValue(CustomXml, "FFXCASIntensity")) is > 0.001 && cas) {
            results.Add(new(GroupLimiter, "Schärfe", CheckStatus.Warning,
                "Das Spiel schärft mit FidelityFX CAS und das Filter-Set zusätzlich mit CAS. Einmal reicht.",
                "CAS im Spiel ausschalten", () => SetXml(CustomXml, "FFXCASIntensity", "0.000000")));
        }
        else if (File.Exists(CustomXml)) {
            results.Add(new(GroupLimiter, "Schärfe", CheckStatus.Ok,
                cas ? "Nur das Filter-Set schärft, passt." : "Spiel und Filter-Set schärfen nicht doppelt."));
        }
    }

    bool ActivePresetUsesCas()
    {
        if (FilterService.Active(game) is not { } variant) {
            return false;
        }
        var preset = Path.Combine(game.Folder, FilterService.PresetFile(variant));
        return File.Exists(preset) && File.ReadLines(preset).FirstOrDefault(l => l.StartsWith("Techniques="))?.Contains("@CAS.fx") == true;
    }

    void CheckFrameGeneration(List<CheckResult> results)
    {
        var problem = GenerateOn ? CheckStatus.Warning : CheckStatus.Info;

        results.Add(new(GroupFg, "Einstellung", GenerateOn && !OutputOn ? CheckStatus.Warning : GenerateOn ? CheckStatus.Ok : CheckStatus.Info,
            GenerateOn && !OutputOn ? "Frame Generation ist an, die dafür nötige D3D12-Ausgabe aber aus. So läuft sie nicht."
                : GenerateOn ? "Frame Generation ist an." : "Frame Generation ist aus.",
            GenerateOn && !OutputOn ? "D3D12-Ausgabe einschalten" : null,
            GenerateOn && !OutputOn ? () => SetEdaa("fg_present_test", "1") : null));

        var gpus = GraphicsAdapters();
        var fit = gpus.FirstOrDefault(g => RtxSeries(g) >= 4);
        if (Vendor.Amd) {
            // AMD FSR Frame Generation runs on any card; no Streamline (edaa/docs/vendors.md).
            results.Add(new(GroupFg, "Grafikkarte", CheckStatus.Info,
                (gpus.Count > 0 ? string.Join(", ", gpus) : "Nicht erkannt") + ". AMD FSR Frame Generation läuft auf jeder Karte."));
        }
        else results.Add(fit != null
            ? new(GroupFg, "Grafikkarte", CheckStatus.Ok, $"{fit}, unterstützt DLSS Frame Generation.")
            : new(GroupFg, "Grafikkarte", problem, (gpus.Count > 0 ? string.Join(", ", gpus) : "Nicht erkannt") + ". Frame Generation braucht eine RTX 40 oder RTX 50."));

        var scheduling = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", null) as int?;
        results.Add(scheduling == 2
            ? new(GroupFg, "GPU-Planung (HAGS)", CheckStatus.Ok, "An.")
            : new(GroupFg, "GPU-Planung (HAGS)", problem, "Aus oder nicht gesetzt. Einschalten unter Windows-Einstellungen → Anzeige → Grafik, danach neu starten."));

        var missing = StreamlineFiles.Where(f => !File.Exists(Path.Combine(game.Folder, f))).ToList();
        if (Vendor.Nvidia) results.Add(missing.Count == 0
            ? new(GroupFg, "Streamline-Dateien", CheckStatus.Ok, "Vollständig.")
            : new(GroupFg, "Streamline-Dateien", problem, $"Es fehlen: {string.Join(", ", missing)}. Unter „Übersicht“ installieren."));

        if (GenerateOn && Edaa("fg_hud_protect") == "1" && Edaa("taa_before_hud") != "1") {
            results.Add(new(GroupFg, "HUD-Schutz", CheckStatus.Info,
                "Wirkt nur voll mit „Resolve before the HUD pass“, das ist gerade aus. Dafür wird das HUD geglättet."));
        }

        // RenoDX is only reported: the user switches it off in the overlay.
        if (OutputOn && InstallInfo.Scan(game.Folder).Any(c => c.Name == "RenoDX" && c.State == ComponentState.Installed)) {
            results.Add(new(GroupFg, "RenoDX", CheckStatus.Warning,
                "RenoDX ist zusammen mit der D3D12-Ausgabe an. Das gibt ein schwarzes Bild. Im Overlay unter „Add-ons“ abschalten."));
        }

        CheckLogs(results);
    }

    void CheckLogs(List<CheckResult> results)
    {
        var states = SharedLines(Path.Combine(game.Folder, "ReShade.log")).Where(l => l.Contains("EDAA FG: DLSS-G state")).ToList();
        if (states.Count > 0) {
            results.Add(states.Any(l => l.Contains("frames presented 2"))
                ? new(GroupFg, "Letzte Sitzung", CheckStatus.Ok, "Laut ReShade.log wurden Bilder generiert.")
                : new(GroupFg, "Letzte Sitzung", GenerateOn ? CheckStatus.Warning : CheckStatus.Info, "Laut ReShade.log wurden keine Bilder generiert."));
        }

        var streamline = SharedLines(Path.Combine(game.Folder, "sl.log")).ToList();
        var hangs = streamline.Count(l => l.Contains("Wait on gpu fence timed out"));
        if (hangs > 0) {
            results.Add(new(GroupFg, "GPU-Hänger", CheckStatus.Warning,
                $"sl.log meldet {hangs}-mal „Wait on gpu fence timed out“. Die GPU hing, ein Absturz folgt meist."));
        }

        if (ActiveFgPreset(game.Folder) is { } preset) {
            var applied = preset.Applied ? "angewendet" : "nicht angewendet";
            var latest = preset.Latest ? "das neueste" : "nicht das neueste";
            results.Add(new(GroupFg, "FG-Preset", CheckStatus.Info, $"{preset.Letter} per Treiber-Override, {applied}, {latest}."));
        }
    }

    // What the driver reported for frame generation in the last session.
    public static (char Letter, bool Applied, bool Latest)? ActiveFgPreset(string gameFolder)
    {
        var match = SharedLines(Path.Combine(gameFolder, "sl.log")).Select(l => FgPresetLine().Match(l)).LastOrDefault(m => m.Success);
        if (match == null || !int.TryParse(match.Groups[1].Value, out var value) || value is < 1 or > 26) {
            return null;
        }
        return ((char)('A' + value - 1), match.Groups[2].Value == "Yes", match.Groups[3].Value == "Yes");
    }

    void CheckFiles(List<CheckResult> results)
    {
        var addons = new[] { "EDAA.addon64", "GenFrame.addon64" }.Where(n => File.Exists(Path.Combine(game.Folder, n))).ToList();
        if (addons.Count > 1) {
            results.Add(new(GroupFiles, "Add-on doppelt", CheckStatus.Warning,
                "EDAA.addon64 und GenFrame.addon64 liegen beide im Spielordner, ReShade lädt beide. Eine davon entfernen."));
        }

        var old = FindOldBuilds();
        results.Add(old.Count == 0
            ? new(GroupFiles, "Alte Test-Builds", CheckStatus.Ok, "Keine gefunden.")
            : new(GroupFiles, "Alte Test-Builds", CheckStatus.Warning, string.Join(", ", old.Select(Path.GetFileName)),
                "Wegräumen", MoveOldBuilds));
    }

    List<string> FindOldBuilds() => Directory.EnumerateFiles(game.Folder)
        .Where(f => OldBuildPatterns.Any(p => FileSystemName.MatchesSimpleExpression(p, Path.GetFileName(f), ignoreCase: true)))
        .ToList();

    // Moved, not deleted: same place as the clean reinstall.
    void MoveOldBuilds()
    {
        var target = Path.Combine(CleanupService.Root, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(target);
        foreach (var file in FindOldBuilds()) {
            var destination = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, destination);
            if (new FileInfo(destination).Length != new FileInfo(file).Length) {
                throw new IOException($"Kopie von {Path.GetFileName(file)} ist unvollständig – Abbruch.");
            }
            File.Delete(file);
        }
    }

    void SetEdaa(string key, string value)
    {
        var ini = IniFile.Load(ReShadeIni);
        ini.Set("EDAA", key, value);
        ini.Save(ReShadeIni);
    }

    public static string? XmlValue(string path, string tag)
    {
        if (!File.Exists(path)) {
            return null;
        }
        var match = Regex.Match(File.ReadAllText(path), $"<{tag}>(.*?)</{tag}>");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    // Only the value changes, the rest of Frontier's file stays as it is.
    public static void SetXml(string path, string tag, string value)
    {
        var text = File.ReadAllText(path);
        var changed = Regex.Replace(text, $"<{tag}>.*?</{tag}>", $"<{tag}>{value}</{tag}>");
        if (changed == text && !text.Contains($"<{tag}>{value}</{tag}>")) {
            throw new InvalidDataException($"{tag} fehlt in {Path.GetFileName(path)}.");
        }
        File.WriteAllText(path, changed, new UTF8Encoding(false));
    }

    static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    // The game may be writing these logs while the app reads them.
    static IEnumerable<string> SharedLines(string path)
    {
        if (!File.Exists(path)) {
            yield break;
        }
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line) {
            yield return line;
        }
    }

    static List<string> GraphicsAdapters()
    {
        var names = new List<string>();
        using var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
        foreach (var name in cls?.GetSubKeyNames().Where(n => n.All(char.IsAsciiDigit)) ?? []) {
            using var adapter = cls!.OpenSubKey(name);
            if (adapter?.GetValue("DriverDesc") is string description) {
                names.Add(description);
            }
        }
        return names;
    }

    // 4 for "RTX 4070 Ti", 0 for anything that is not a four-digit RTX.
    static int RtxSeries(string adapter)
    {
        var match = RtxName().Match(adapter);
        return match.Success ? match.Groups[1].Value[0] - '0' : 0;
    }

    [GeneratedRegex(@"RTX\s*(\d{4})\b")]
    private static partial Regex RtxName();

    [GeneratedRegex(@"(?m)^Limit=(\d+)")]
    private static partial Regex RtssLimitLine();

    [GeneratedRegex(@"(?m)^LimitDenominator=(\d+)")]
    private static partial Regex RtssDenominatorLine();

    [GeneratedRegex(@"\[FrameGeneration\] Override Reported: ModelPreset, Value: (\d+), Requested: \w+, Applied: (\w+), Is Latest: (\w+)")]
    private static partial Regex FgPresetLine();
}
