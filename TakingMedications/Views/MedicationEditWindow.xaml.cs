using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class MedicationEditWindow : Window
{
    private readonly Medication _med;
    private readonly bool _isNew;

    public MedicationEditWindow(Medication med, IReadOnlyList<MedicationSection> allSections, bool isNew)
    {
        InitializeComponent();

        _med = med;
        _isNew = isNew;

        SectionCombo.ItemsSource = allSections;
        SectionCombo.SelectedValue = med.SectionKey ?? allSections.FirstOrDefault()?.SectionKey;

        TimeBox.Text        = med.Time;
        TimeNoteBox.Text    = med.TimeNote;
        NameBox.Text        = med.Name;
        SubtitleBox.Text    = med.Subtitle;
        NoteBox.Text        = med.Note;
        DoctorBox.Text      = med.Doctor;
        CourseBox.Text      = med.Course;
        PharmacyBox.Text    = med.Pharmacy;
        IdBox.Text          = med.Id;
        DescriptionBox.Text = med.Description;

        IdBox.IsReadOnly = !isNew; // ID не меняем после создания — на него ссылаются отметки в state
        if (!isNew) IdBox.Opacity = 0.6;

        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
        Closed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        var header = _isNew
            ? Loc.T("edit_add_title")
            : Loc.T("edit_edit_title", ("name", _med.Name));
        HeaderLabel.Text = header;
        Title = header;

        LblSection.Text     = Loc.T("edit_section");
        LblTime.Text        = Loc.T("edit_time");
        LblTimeNote.Text    = Loc.T("edit_time_note");
        LblName.Text        = Loc.T("edit_name");
        LblSubtitle.Text    = Loc.T("edit_subtitle");
        LblNote.Text        = Loc.T("edit_note");
        LblDoctor.Text      = Loc.T("edit_doctor");
        LblCourse.Text      = Loc.T("edit_course");
        LblPharmacy.Text    = Loc.T("edit_pharmacy");
        LblId.Text          = Loc.T("edit_id");
        LblDescription.Text = Loc.T("edit_description");
        BtnSave.Content     = Loc.T("btn_save");
        BtnCancel.Content   = Loc.T("btn_cancel");
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this,
                Loc.T("edit_name_required"),
                Loc.T("warning_title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        _med.SectionKey  = (string)SectionCombo.SelectedValue ?? _med.SectionKey ?? "";
        _med.Time        = TimeBox.Text.Trim();
        _med.TimeNote    = TimeNoteBox.Text.Trim();
        _med.Name        = NameBox.Text.Trim();
        _med.Subtitle    = SubtitleBox.Text.Trim();
        _med.Note        = NoteBox.Text.Trim();
        _med.Doctor      = DoctorBox.Text.Trim();
        _med.Course      = CourseBox.Text.Trim();
        _med.Pharmacy    = PharmacyBox.Text.Trim();
        _med.Description = DescriptionBox.Text;
        if (_isNew) _med.Id = IdBox.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
