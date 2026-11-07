using MdReader.ViewModels;

namespace MdReader.Tests.ViewModels;

public sealed class LinkDialogViewModelTests
{
    [Theory]
    [InlineData("https://exemple.fr", true)]
    [InlineData("http://exemple.fr/page", true)]
    [InlineData("mailto:contact@exemple.fr", true)]
    [InlineData("https://", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("exemple.fr", false)]
    public void Valide_l_adresse_saisie(string url, bool expected)
    {
        var viewModel = new LinkDialogViewModel { Url = url };

        Assert.Equal(expected, viewModel.CanConfirm);
    }
}
