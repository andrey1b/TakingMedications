using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class PurchaseEditDialog : Window
{
    public PurchaseEntry Result { get; private set; } = new();

    public PurchaseEditDialog(IEnumerable<MedicationSection> sections, PurchaseEntry? existing = null)
    {
        InitializeComponent();

        var allMeds = sections.SelectMany(s => s.Items).OrderBy(m => m.Name).ToList();
        MedCombo.ItemsSource = allMeds;

        if (existing != null)
        {
            Result = existing;
            if (DateTime.TryParse(existing.Date, out var d)) DatePickerCtl.SelectedDate = d;
            else DatePickerCtl.SelectedDate = DateTime.Today;
            MedCombo.SelectedValue = existing.MedId;
            AmountBox.Text = existing.Amount.ToString(CultureInfo.InvariantCulture);
            NoteBox.Text = existing.Note ?? "";
        }
        else
        {
            DatePickerCtl.SelectedDate = DateTime.Today;
            if (allMeds.Count > 0) MedCombo.SelectedIndex = 0;
        }

        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
        Closed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        Title = Loc.T("finance_add_purchase");
        HeaderLabel.Text = Loc.T("finance_add_purchase");
        LblDate.Text   = Loc.T("finance_date");
        LblMed.Text    = Loc.T("finance_med");
        LblAmount.Text = Loc.T("finance_amount");
        LblNote.Text   = Loc.T("finance_note");
        BtnSave.Content = Loc.T("btn_save");
        BtnCancel.Content = Loc.T("btn_cancel");
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var medId = MedCombo.SelectedValue as string;
        if (string.IsNullOrEmpty(medId))
        {
            MessageBox.Show(this, Loc.T("finance_med_required"),
                Loc.T("warning_title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var raw = AmountBox.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0)
        {
            MessageBox.Show(this, Loc.T("finance_amount_required"),
                Loc.T("warning_title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            AmountBox.Focus();
            return;
        }

        var date = DatePickerCtl.SelectedDate ?? DateTime.Today;
        Result.Date   = date.ToString("yyyy-MM-dd");
        Result.MedId  = medId;
        Result.Amount = amount;
        var note = NoteBox.Text.Trim();
        Result.Note = string.IsNullOrEmpty(note) ? null : note;

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
