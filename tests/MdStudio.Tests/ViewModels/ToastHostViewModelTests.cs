using MdStudio.Models;
using MdStudio.ViewModels;

namespace MdStudio.Tests.ViewModels;

public sealed class ToastHostViewModelTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly ToastHostViewModel _host;

    public ToastHostViewModelTests() => _host = new ToastHostViewModel(_time, () => false);

    [Fact]
    public void Ferme_chaque_type_apres_sa_duree_et_garde_l_erreur()
    {
        Show(FeedbackType.Success, "Succès");
        Show(FeedbackType.Info, "Information");
        Show(FeedbackType.Warning, "Avertissement");
        Show(FeedbackType.Danger, "Erreur");

        _time.Advance(TimeSpan.FromMilliseconds(4500));
        Assert.Equal(["Information", "Avertissement", "Erreur"], Titles());

        _time.Advance(TimeSpan.FromMilliseconds(1500));
        Assert.Equal(["Avertissement", "Erreur"], Titles());

        _time.Advance(TimeSpan.FromMilliseconds(3000));
        Assert.Equal(["Erreur"], Titles());

        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(["Erreur"], Titles());
    }

    [Fact]
    public void Reprend_le_minuteur_au_temps_restant_apres_le_survol()
    {
        Show(FeedbackType.Warning, "Avertissement");
        var toast = _host.Toasts[0];

        _time.Advance(TimeSpan.FromSeconds(3));
        toast.Pause();
        _time.Advance(TimeSpan.FromSeconds(30));
        toast.Resume();
        _time.Advance(TimeSpan.FromMilliseconds(5900));
        Assert.Single(_host.Toasts);

        _time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Empty(_host.Toasts);
    }

    [Fact]
    public void Garde_quatre_toasts_et_retire_le_plus_ancien_qui_n_est_pas_une_erreur()
    {
        Show(FeedbackType.Danger, "Erreur");
        for (var i = 1; i <= 4; i++)
        {
            Show(FeedbackType.Success, $"Succès {i}");
        }

        Assert.Equal(["Erreur", "Succès 2", "Succès 3", "Succès 4"], Titles());
    }

    [Fact]
    public void Retire_la_plus_ancienne_erreur_quand_il_n_y_a_que_des_erreurs()
    {
        for (var i = 1; i <= 5; i++)
        {
            Show(FeedbackType.Danger, $"Erreur {i}");
        }

        Assert.Equal(["Erreur 2", "Erreur 3", "Erreur 4", "Erreur 5"], Titles());
    }

    [Fact]
    public void Remplace_un_message_repete_et_relance_son_minuteur()
    {
        for (var i = 0; i < 10; i++)
        {
            Show(FeedbackType.Success, "Document enregistré");
            _time.Advance(TimeSpan.FromMilliseconds(500));
        }

        Assert.Single(_host.Toasts);
        Assert.Equal(9, _host.Toasts[0].TimerRun);

        _time.Advance(TimeSpan.FromMilliseconds(3900));
        Assert.Single(_host.Toasts);

        _time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Empty(_host.Toasts);
    }

    [Fact]
    public void Empile_un_message_repete_apres_deux_secondes()
    {
        Show(FeedbackType.Info, "Document enregistré");
        _time.Advance(TimeSpan.FromMilliseconds(2500));

        Show(FeedbackType.Info, "Document enregistré");

        Assert.Equal(2, _host.Toasts.Count);
    }

    [Fact]
    public void Execute_l_action_puis_ferme_seulement_son_toast()
    {
        var ran = false;
        Show(FeedbackType.Danger, "Autre erreur");
        _host.Show(new ResultError(FeedbackType.Danger, "Enregistrement impossible", "a.md n'a pas pu être écrit."), [new ToastAction("Réessayer", true, () => ran = true)]);

        _host.Toasts[1].Actions[0].Command.Execute(null);

        Assert.True(ran);
        Assert.Equal(["Autre erreur"], Titles());
    }

    [Fact]
    public void Retire_le_toast_apres_l_animation_de_fermeture()
    {
        var host = new ToastHostViewModel(_time, () => true);
        host.Show(new ResultError(FeedbackType.Danger, "Erreur", "Message."));
        var toast = host.Toasts[0];

        toast.CloseCommand.Execute(null);
        Assert.True(toast.IsClosing);
        Assert.Single(host.Toasts);

        _time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Empty(host.Toasts);
    }

    [Fact]
    public void Porte_le_type_par_le_kicker_et_le_role_d_accessibilite()
    {
        Show(FeedbackType.Success, "Diagramme validé");
        Show(FeedbackType.Danger, "Fichier illisible");
        var (success, error) = (_host.Toasts[0], _host.Toasts[1]);

        Assert.Equal("SUCCÈS", success.Kicker);
        Assert.Equal("ERREUR", error.Kicker);
        Assert.False(success.IsAlert);
        Assert.True(error.IsAlert);
        Assert.False(error.HasTimer);
        Assert.Equal("Erreur : Fichier illisible. Message.", error.AccessibleText);
    }

    [Fact]
    public void Place_l_action_principale_en_premier_et_refuse_une_troisieme_action()
    {
        _host.Show(
            new ResultError(FeedbackType.Warning, "Avertissement", "Message."),
            [new ToastAction("Plus tard", false, () => { }), new ToastAction("Fermer", true, () => { })]);

        Assert.Equal(["Fermer", "Plus tard"], _host.Toasts[0].Actions.Select(a => a.Label));
        Assert.Throws<ArgumentOutOfRangeException>(() => _host.Show(
            new ResultError(FeedbackType.Info, "Info", "Message."),
            [new ToastAction("A", false, () => { }), new ToastAction("B", false, () => { }), new ToastAction("C", false, () => { })]));
    }

    [Fact]
    public void Affiche_sur_le_meme_thread_meme_avec_un_autre_contexte_de_synchronisation()
    {
        var original = SynchronizationContext.Current;
        try
        {
            // WPF hands out a new DispatcherSynchronizationContext instance per dispatcher operation.
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            var host = new ToastHostViewModel(_time, () => false);
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            host.Show(new ResultError(FeedbackType.Info, "Document enregistré", "a.md est enregistré sur le disque."));

            Assert.Single(host.Toasts);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    private void Show(FeedbackType type, string title) => _host.Show(new ResultError(type, title, "Message."));

    private string[] Titles() => [.. _host.Toasts.Select(t => t.Title)];
}
