using System.Windows;
using System.Windows.Controls;
using CodeTyper.Wpf.Models;

namespace CodeTyper.Wpf;

public partial class WordEditDialog : Window
{
    public WordUpsertRequest? Result { get; private set; }

    public WordEditDialog(WordEntry? existing)
    {
        InitializeComponent();
        if (existing is not null)
        {
            TitleText.Text = "単語を編集";
            WordBox.Text = existing.Word;
            SelectComboByContent(LangCombo, existing.Language);
            SelectComboByContent(DiffCombo, existing.Difficulty);
            WeightBox.Text = existing.Weight.ToString();
            EnabledCheck.IsChecked = existing.Enabled;
        }
    }

    private static void SelectComboByContent(ComboBox combo, string value)
    {
        foreach (ComboBoxItem item in combo.Items)
            if (item.Content?.ToString() == value) { item.IsSelected = true; return; }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var word = WordBox.Text.Trim();
        if (string.IsNullOrEmpty(word)) { MessageBox.Show("単語を入力してください"); return; }
        if (!int.TryParse(WeightBox.Text, out var weight) || weight < 1 || weight > 10)
        { MessageBox.Show("重みは1〜10の整数で入力してください"); return; }

        var lang = (LangCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "java";
        var diff = (DiffCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "easy";

        Result = new WordUpsertRequest(word, lang, diff, weight, EnabledCheck.IsChecked == true);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
