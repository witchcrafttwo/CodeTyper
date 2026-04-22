using System.Windows;
using System.Windows.Controls;

namespace CodeTyper.Wpf;

public partial class HomeWindow : Window
{
    public HomeWindow()
    {
        InitializeComponent();
        PlayerNameBox.Focus();
    }

    private bool Validate(out string playerName)
    {
        playerName = PlayerNameBox.Text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            MessageBox.Show("Please enter your player name.", "Notice",
                MessageBoxButton.OK, MessageBoxImage.Information);
            PlayerNameBox.Focus();
            return false;
        }
        return true;
    }

    private void GlobalBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!Validate(out var name)) return;
        OpenGame(name, "global", null);
    }

    private void GroupBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!Validate(out var name)) return;
        var groupId = GroupIdBox.Text.Trim();
        if (string.IsNullOrEmpty(groupId))
        {
            MessageBox.Show("Please enter a Group ID.", "Notice",
                MessageBoxButton.OK, MessageBoxImage.Information);
            GroupIdBox.Focus();
            return;
        }
        OpenGame(name, "team", groupId);
    }

    private void OpenGame(string playerName, string scope, string? groupId)
    {
        var main = new MainWindow(playerName, scope, groupId);
        main.Show();
        Close();
    }

    private void RankingBtn_Click(object sender, RoutedEventArgs e)
    {
        var main = new MainWindow(null, "global", null, openRanking: true);
        main.Show();
        Close();
    }
}
