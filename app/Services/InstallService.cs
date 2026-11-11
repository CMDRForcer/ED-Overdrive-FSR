using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GenFrame.Setup.Services;

public enum Channel { Stable, Development }

// Installs ReShade (add-on build), EDAA and Streamline into the game folder. Both ship inside the exe
// (ThirdParty/fetch-bundle.ps1; EDAA is built from edaa/ in this repository), so installing needs no
// internet; only the development channel and the filters download.
// Sources: see docs/tool-research.md and THIRD_PARTY_NOTICES.md.
public sealed class InstallService(GameInstall game, Action<string> log)
{
    // Keep in step with ThirdParty/fetch-bundle.ps1 (which pins the installer by SHA-256).
    public const string BundledReShade = "6.8.0";
    public const string LicenseFolder = "ED-Overdrive-Lizenzen";

    // The EDAA version inside the exe (version.txt, written by edaa/tools/bundle.ps1).
    public static string BundledEdaa { get; } = ReadBundledEdaa();

    const string Repository = "CMDRForcer/ED-Overdrive";
    const string DevBranch = "builds/main"; // CI build of every push to main, before the next release
    const string AddonName = "EDAA.addon64";

    static readonly string[] PackageFiles = [
        AddonName, "nvngx_dlss.dll", "nvngx_dlssg.dll", "libxess.dll",
        "amd_fidelityfx_loader_dx12.dll", "amd_fidelityfx_upscaler_dx12.dll",
        "sl.common.dll", "sl.dlss_g.dll", "sl.interposer.dll", "sl.pcl.dll", "sl.reflex.dll",
    ];
    static readonly string[] DevFiles = [
        AddonName, "streamline/nvngx_dlssg.dll", "streamline/sl.common.dll", "streamline/sl.dlss_g.dll",
        "streamline/sl.interposer.dll", "streamline/sl.pcl.dll", "streamline/sl.reflex.dll",
    ];

    static readonly HttpClient http = CreateClient();

    static string DownloadFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenFrame", "Downloads");

    public async Task Install(Channel channel, bool clean)
    {
        EnsureGameClosed();
        Directory.CreateDirectory(DownloadFolder);

        var reshade = BundledReShadeDll();
        // Download first: a broken connection must not leave a half-installed game.
        (string Folder, string Commit)? dev = channel == Channel.Development ? await DownloadDevBuild() : null;

        EnsureGameClosed();
        var disabledAddons = ReadDisabledAddons();

        var replaced = PackageFiles.Append("dxgi.dll").Select(f => Path.Combine(game.Folder, f));
        var backup = BackupService.Create(clean ? "Vor sauberer Neuinstallation" : "Vor Installation",
            Pages.BackupsPage.FilesToSave(game).Concat(replaced));
        log($"Backup angelegt: {backup.Files.Count} Dateien.");

        if (clean) {
            CleanupService.MoveOldFiles(game, log);
        }

        File.WriteAllBytes(Path.Combine(game.Folder, "dxgi.dll"), reshade);
        log($"ReShade {BundledReShade} (Add-on-Version) installiert.");

        using (var zip = new ZipArchive(Resource("EDAA.zip"))) {
            foreach (var name in PackageFiles) {
                var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"{name} fehlt im Paket.");
                entry.ExtractToFile(Path.Combine(game.Folder, name), overwrite: true);
            }
            // The licenses of everything the package brings (EDAA, ReShade, Streamline, DLSS, XeSS, AMD FidelityFX, ImGui).
            var licenses = Path.Combine(game.Folder, LicenseFolder);
            Directory.CreateDirectory(licenses);
            foreach (var entry in zip.Entries.Where(e => e.Name.Length > 0 &&
                         (e.FullName.Replace('\\', '/').StartsWith("licenses/") || e.FullName is "LICENSE" or "THIRD_PARTY_NOTICES.md"))) {
                var name = entry.FullName == "LICENSE" ? "EDAA-LICENSE.txt" : entry.Name;
                entry.ExtractToFile(Path.Combine(licenses, name), overwrite: true);
            }
        }
        log($"EDAA {BundledEdaa} installiert (DLAA, XeSS, AMD FSR experimentell, Streamline), aus der App. Lizenzen in „{LicenseFolder}“.");

        if (dev != null) {
            foreach (var file in Directory.EnumerateFiles(dev.Value.Folder)) {
                File.Copy(file, Path.Combine(game.Folder, Path.GetFileName(file)), overwrite: true);
            }
            log($"Entwicklungs-Build {dev.Value.Commit[..7]} darübergelegt.");
        }

        WriteReShadeIni(clean, disabledAddons);
        FilterService.WriteFgWindowConfig(game);
        log("ReShade2.ini für das FG-Fenster angelegt (ohne Effekte).");

        // A clean install ends ready to play: with the default filter variant.
        if (clean) {
            await new FilterService(game, log).Install(FilterVariant.Vivid);
        }
        log("Fertig. Elite kann gestartet werden.");
    }

    void EnsureGameClosed()
    {
        if (GameState.IsRunning()) {
            throw new InvalidOperationException("Elite läuft. Bitte das Spiel beenden und erneut versuchen.");
        }
    }

    static Stream Resource(string name) =>
        typeof(InstallService).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"{name} fehlt in dieser App-Version.");

    static string ReadBundledEdaa()
    {
        try {
            using var zip = new ZipArchive(Resource("EDAA.zip"));
            var entry = zip.GetEntry("version.txt");
            if (entry == null) {
                return "0.7.0"; // packages from before version.txt (EDAA v0.7.0-cmdr1)
            }
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd().Trim();
        }
        catch (InvalidDataException) {
            return "?";
        }
    }

    static byte[] BundledReShadeDll()
    {
        using var installer = Resource("ReShade_Setup.exe");
        using var copy = new MemoryStream();
        installer.CopyTo(copy);

        // Same as the ReShade installer: a zip is appended to the executable, starting at a 512-byte block.
        var data = copy.ToArray();
        for (int offset = 0; offset + 30 <= data.Length; offset += 512) {
            if (data[offset] == 0x50 && data[offset + 1] == 0x4B && data[offset + 2] == 0x03 && data[offset + 3] == 0x04
                && data.AsSpan(offset + 4, 26).IndexOfAnyExcept((byte)0) >= 0) {
                using var zip = new ZipArchive(new MemoryStream(data, offset, data.Length - offset));
                var entry = zip.GetEntry("ReShade64.dll") ?? throw new InvalidDataException("ReShade64.dll fehlt im Installer.");
                using var stream = entry.Open();
                using var dll = new MemoryStream();
                stream.CopyTo(dll);
                return dll.ToArray();
            }
        }
        throw new InvalidDataException("Im ReShade-Installer wurde kein Archiv gefunden.");
    }

    async Task<(string Folder, string Commit)> DownloadDevBuild()
    {
        string Raw(string path) => $"https://raw.githubusercontent.com/{Repository}/{DevBranch}/{path}";

        var commit = (await http.GetStringAsync(Raw("source_commit.txt"))).Trim();
        var folder = Path.Combine(DownloadFolder, "dev-" + commit[..7]);
        if (!Directory.Exists(folder)) {
            log($"Lade Entwicklungs-Build {commit[..7]} …");
            var temp = folder + ".part";
            Directory.CreateDirectory(temp);
            foreach (var path in DevFiles) {
                await DownloadTo(Raw(path), Path.Combine(temp, Path.GetFileName(path)));
            }
            // The DLSS DLL EDAA was built and tested with; older builds branches do not have it yet.
            try {
                await DownloadTo(Raw("nvngx_dlss.dll"), Path.Combine(temp, "nvngx_dlss.dll"));
            }
            catch (HttpRequestException) {
                log("nvngx_dlss.dll nicht im Entwicklungs-Build, die aus dem Paket bleibt.");
            }
            Directory.Move(temp, folder);
        }
        return (folder, commit);
    }

    List<string> ReadDisabledAddons()
    {
        var ini = Path.Combine(game.Folder, "ReShade.ini");
        if (!File.Exists(ini)) {
            return [];
        }
        var line = File.ReadLines(ini).FirstOrDefault(l => l.StartsWith("DisabledAddons="));
        return line == null ? [] : line["DisabledAddons=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    // Clean: a new ini with the defaults of docs/plan.md. Update: only add [EDAA] keys that are missing,
    // the user's own values stay.
    void WriteReShadeIni(bool clean, List<string> disabledAddons)
    {
        var path = Path.Combine(game.Folder, "ReShade.ini");
        var ini = clean || !File.Exists(path) ? new IniFile() : IniFile.Load(path);

        if (clean) {
            // Keep foreign add-ons (RenoDX) in the state the user chose.
            if (disabledAddons.Count > 0) {
                ini.Set("ADDON", "DisabledAddons", string.Join(",", disabledAddons));
            }
            ini.Set("GENERAL", "EffectSearchPaths", @".\reshade-shaders\Shaders\**");
            ini.Set("GENERAL", "TextureSearchPaths", @".\reshade-shaders\Textures\**");
            ini.Set("GENERAL", "SkipLoadingDisabledEffects", "0");
            ini.Set("GENERAL", "PreprocessorDefinitions", FilterService.DepthDefinitions);
            ini.Set("INPUT", "KeyOverlay", "36,0,0,0");
            ini.Set("OVERLAY", "TutorialProgress", "4");
        }

        foreach (var (key, value) in Defaults) {
            if (clean || ini.Get("EDAA", key) == null) {
                ini.Set("EDAA", key, value);
            }
        }
        ini.Save(path);
        log(clean ? "ReShade.ini mit Standardwerten neu angelegt." : "ReShade.ini ergänzt (deine Werte bleiben).");
    }

    // docs/plan.md: DLAA, model K, full FG HUD protection, frame generation on.
    // hud_learn/hud_motion: find the cockpit HUD by its panel textures (EDHM replaces the HUD shaders) and give the
    // panels the cockpit's motion; sharp panels in motion, ~0.05 ms (edaa/docs/fg-notes.md).
    static readonly (string Key, string Value)[] Defaults = [
        ("aa_method", "1"), ("dlss_preset", "11"), ("taa_before_hud", "1"), ("fg_hud_protect", "1"),
        ("hud_learn", "1"), ("hud_motion", "1"),
        ("fg_present_test", "1"), ("fg_factory", "1"), ("fg_generate", "1"), ("fg_fps_limit", "140"),
    ];

    async Task DownloadTo(string url, string file)
    {
        var temp = file + ".part";
        using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)) {
            response.EnsureSuccessStatusCode();
            await using var output = File.Create(temp);
            await response.Content.CopyToAsync(output);
        }
        File.Move(temp, file, overwrite: true);
    }

    static string Sha256(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ED-Overdrive/0.1");
        return client;
    }
}
