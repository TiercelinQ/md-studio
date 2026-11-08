# MdReader

## Origin

Framework: dotnet-wpf v1.2.0

## Business context

Lecteur Markdown de bureau pour Windows (interface en français). Il ouvre un fichier `.md` / `.markdown` ou un dossier entier, rend le document (tableaux, code coloré, cases de tâches, diagrammes Mermaid), aide à s'y repérer (onglets, plan, recherche, zoom) et permet de le corriger en WYSIWYG avec une barre de 17 commandes. L'enregistrement ne réécrit que les blocs modifiés : le reste du fichier est conservé à l'octet près.

Référence fonctionnelle : `../new-markdown-note/specs/cahier-des-charges-lecteur-markdown.md` et sa maquette. Spécifications : `docs/specs/01` à `04` (le contrat verrouillé est `04-architect.md`).

## Deviations from the framework

- E-01 : ~25 jetons de couleur, rayons et échelle typographique du cahier — reason: maquette validée par le demandeur.
- E-02 : pas de Mica — reason: DS-04 du cahier.
- E-03 : icônes filaires de l'annexe A (`Themes/Icons.xaml`) au lieu de Segoe Fluent Icons — reason: §5.6 du cahier.
- E-04 : barre de titre portant les onglets de documents, pas de NavigationView — reason: T-08, zone A.
- E-05 : notifications en bas au centre, une seule à la fois, information 3,2 s, erreur jusqu'au ×, sans icône — reason: F-NOT, phase 3.
- E-06 : boîtes de dialogue sans ×, voile non fermant, boutons de largeurs égales — reason: F-ENR-04, C-19.
- E-07 : bandeau en ligne « Le fichier a changé sur le disque. » — reason: choix de la phase 2.
- E-08 : ouverture d'URL `http` / `https` issues d'un document par le shell — reason: F-LEC-04.
- E-09 : liens internes et images résolus sans confinement à une racine, restreints par extension, en lecture seule — reason: liens `../` (F-LEC-03, F-LEC-06).
- E-10 : `IDocumentSurface`, interface implémentée par la vue et tenue par le ViewModel — reason: pont asynchrone avec la page WebView2.
- E-11 : `DialogService` dans `Services/` avec des types WPF — reason: adaptateur UI comme `FeedbackService`.
- E-12 : seuils 820 et 560 dans `AppConfig` — reason: ils pilotent un comportement.
- E-13 : xunit.v3 4.0.2 — reason: version stable confirmée en phase 1.
- E-14 : `IDialogService.ShowContentAsync` générique, `LinkDialogContent` affiché par `DataTemplate` — reason: pas de dépendance de `Services/` vers `ViewModels/`.
- E-15 : `AppConfig.AccentColorDark` et bornes `ExplorerWidth*` — reason: accent sombre explicite, bornes utilisées par le ViewModel.
- E-16 : highlight.js pris dans `@highlightjs/cdn-assets` — reason: seul paquet npm fournissant le build navigateur.
- E-17 : types d'appui déclarés dans des fichiers existants — reason: aucun fichier ajouté.
- E-18 : cible `net10.0-windows10.0.19041.0` — reason: `WebView2CompositionControl` exige la projection Windows Runtime (sans lui, notifications et boîtes de dialogue passent sous le document).
- E-19 : `global.json` (`dotnet test` sur Microsoft.Testing.Platform) — reason: exigence de xUnit v3 4.x avec le SDK .NET 10 ; commande `dotnet test --solution MdReader.sln`.
- E-20 : données dans `%APPDATA%\MdReader`, partagé avec l'ancienne application du même nom — reason: choix de l'utilisateur, MdReader la remplace.
- E-21 : interfaces existantes étendues (`IDocumentSurface`, `ITextFileService`, `IDialogService`) - reason: mode Source et création de fichier portés par les services existants, sans nouveau fichier.
- E-22 : colonne du document sans largeur maximale (le cahier §5.4 fixe 760 px) - reason: demande de l'utilisateur.
- E-23 : icône `IconFileNewGeometry` hors annexe A - reason: bouton « Créer un fichier », même tracé que les icônes du cahier.

## Maintenance

- Load the project first: `/wpf-load-project`
- Change it: `/wpf-add-feature` · `/wpf-fix-issue` · `/wpf-refactor-code` (each records the change under `[Unreleased]` in `docs/release/CHANGELOG.md`; the version does not move)
- Verify: `/wpf-run-tests`
- Publish a version: `/wpf-release` (turns the accumulated `[Unreleased]` changelog into a dated version and raises the version number)
