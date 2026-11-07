using MdReader.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MdReader.Tests.Services;

public sealed class ExternalLauncherTests
{
    [Theory]
    [InlineData("https://exemple.fr/page")]
    [InlineData("http://exemple.fr")]
    public void Accepte_les_adresses_web_absolues(string url) => Assert.True(ExternalLauncher.IsAllowed(url, out _));

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:")]
    [InlineData("docs/page.md")]
    [InlineData("C:\\Windows\\notepad.exe")]
    public void Refuse_toute_autre_adresse(string url) => Assert.False(ExternalLauncher.IsAllowed(url, out _));

    [Fact]
    public void Retourne_un_echec_sans_rien_lancer_pour_une_adresse_refusee()
    {
        var result = new ExternalLauncher(NullLogger<ExternalLauncher>.Instance).Open("javascript:alert(1)");

        Assert.False(result.Ok);
    }
}
