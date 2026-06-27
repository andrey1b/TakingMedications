using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class InteractionsDialog : Window
{
    public InteractionsDialog(IEnumerable<MedicationSection> sections)
    {
        InitializeComponent();

        var hits = DrugInteractions.CheckAll(sections);
        var allNames = sections.SelectMany(s => s.Items).Select(m => m.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        int nMeds  = allNames.Count;
        int nPairs = nMeds * (nMeds - 1) / 2;

        ApplyLocalization(hits, nMeds, nPairs);
        Loc.LanguageChanged += () => ApplyLocalization(hits, nMeds, nPairs);
        Closed += (_, _) => Loc.LanguageChanged -= () => ApplyLocalization(hits, nMeds, nPairs);

        if (hits.Count > 0)
        {
            InteractionsList.ItemsSource = hits.Select(h => new HitVm(h)).ToList();
            NoHitsLabel.Visibility = Visibility.Collapsed;
        }
        else
        {
            InteractionsList.Visibility = Visibility.Collapsed;
            NoHitsLabel.Visibility = Visibility.Visible;
        }
    }

    private void ApplyLocalization(List<DrugInteractions.InteractionHit> hits, int nMeds, int nPairs)
    {
        Title = Loc.T("interactions_title");
        HeaderLabel.Text = Loc.T("interactions_title");
        DisclaimerLabel.Text = Loc.T("interactions_disclaimer");
        SummaryLabel.Text = Loc.T("interactions_summary",
            ("n_meds", nMeds), ("n_pairs", nPairs),
            ("n_rules", DrugInteractions.RulesCount), ("n_hits", hits.Count));
        NoHitsLabel.Text = Loc.T("interactions_none");
        BtnClose.Content = Loc.T("btn_close");
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private class HitVm
    {
        public string NameA { get; }
        public string NameB { get; }
        public string Note  { get; }
        public string SeverityLabel { get; }
        public string SeverityBg    { get; }

        public HitVm(DrugInteractions.InteractionHit h)
        {
            NameA = h.NameA;
            NameB = h.NameB;
            Note  = h.Note;
            (SeverityLabel, SeverityBg) = h.Severity switch
            {
                DrugInteractions.Severity.High   => (Loc.T("interactions_sev_high"),   "#C62828"),
                DrugInteractions.Severity.Medium  => (Loc.T("interactions_sev_medium"), "#EF6C00"),
                _                                 => (Loc.T("interactions_sev_info"),   "#1565C0"),
            };
        }
    }
}
