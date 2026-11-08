using System.IO;
using MdStudio.Models;
using MdStudio.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MdStudio.Tests.Services;

public sealed class PreferencesServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mdstudio-prefs-" + Guid.NewGuid().ToString("N"));
    private readonly IAppPaths _paths = Substitute.For<IAppPaths>();

    public PreferencesServiceTests()
    {
        Directory.CreateDirectory(_folder);
        _paths.UserData.Returns(_folder);
        _paths.PreferencesFile.Returns(Path.Combine(_folder, "preferences.json"));
        _paths.LegacyPreferencesFile.Returns(Path.Combine(_folder, "ancien", "preferences.json"));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task Utilise_les_valeurs_par_defaut_sans_fichier()
    {
        using var service = new PreferencesService(_paths, NullLogger<PreferencesService>.Instance);

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppConfig.ExplorerWidthDefault, service.Current.ExplorerWidth);
        Assert.Equal(1.0, service.Current.Zoom);
        Assert.Null(service.Current.Theme);
    }

    [Fact]
    public async Task Relit_les_valeurs_ecrites()
    {
        using (var writer = new PreferencesService(_paths, NullLogger<PreferencesService>.Instance))
        {
            await writer.UpdateAsync(p => p with { Zoom = 1.3, Theme = AppTheme.Dark, LastFolder = "C:\\docs" }, TestContext.Current.CancellationToken);
        }

        using var reader = new PreferencesService(_paths, NullLogger<PreferencesService>.Instance);
        await reader.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1.3, reader.Current.Zoom);
        Assert.Equal(AppTheme.Dark, reader.Current.Theme);
        Assert.Equal("C:\\docs", reader.Current.LastFolder);
    }

    [Fact]
    public async Task Garde_les_valeurs_par_defaut_des_cles_absentes()
    {
        await File.WriteAllTextAsync(_paths.PreferencesFile, "{ \"theme\": \"Dark\", \"recentFiles\": [] }", TestContext.Current.CancellationToken);
        using var service = new PreferencesService(_paths, NullLogger<PreferencesService>.Instance);

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppTheme.Dark, service.Current.Theme);
        Assert.Equal(AppConfig.ExplorerWidthDefault, service.Current.ExplorerWidth);
        Assert.Equal(1.0, service.Current.Zoom);
    }

    [Fact]
    public async Task Revient_aux_valeurs_par_defaut_sur_un_fichier_corrompu()
    {
        await File.WriteAllTextAsync(_paths.PreferencesFile, "{ pas du json", TestContext.Current.CancellationToken);
        using var service = new PreferencesService(_paths, NullLogger<PreferencesService>.Instance);

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1.0, service.Current.Zoom);
    }

    [Fact]
    public async Task Borne_le_zoom_et_la_largeur_de_l_explorateur()
    {
        using var service = new PreferencesService(_paths, NullLogger<PreferencesService>.Instance);

        await service.UpdateAsync(p => p with { Zoom = 5, ExplorerWidth = 10 }, TestContext.Current.CancellationToken);

        Assert.Equal(AppConfig.ZoomMax, service.Current.Zoom);
        Assert.Equal(AppConfig.ExplorerWidthMin, service.Current.ExplorerWidth);
    }

    [Fact]
    public async Task Reprend_les_preferences_de_l_ancien_nom_au_premier_lancement()
    {
        await WriteLegacyAsync("{ \"zoom\": 1.4, \"theme\": \"Dark\" }");
        using var service = new PreferencesService(_paths, NullLogger<PreferencesService>.Instance);

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1.4, service.Current.Zoom);
        Assert.Equal(AppTheme.Dark, service.Current.Theme);
        Assert.True(File.Exists(_paths.PreferencesFile));
        Assert.True(File.Exists(_paths.LegacyPreferencesFile));
    }

    [Fact]
    public async Task Ignore_l_ancien_fichier_quand_les_preferences_existent()
    {
        await WriteLegacyAsync("{ \"zoom\": 1.4 }");
        await File.WriteAllTextAsync(_paths.PreferencesFile, "{ \"zoom\": 0.8 }", TestContext.Current.CancellationToken);
        using var service = new PreferencesService(_paths, NullLogger<PreferencesService>.Instance);

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0.8, service.Current.Zoom);
        Assert.Equal("{ \"zoom\": 0.8 }", await File.ReadAllTextAsync(_paths.PreferencesFile, TestContext.Current.CancellationToken));
    }

    private async Task WriteLegacyAsync(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.LegacyPreferencesFile)!);
        await File.WriteAllTextAsync(_paths.LegacyPreferencesFile, json, TestContext.Current.CancellationToken);
    }
}
