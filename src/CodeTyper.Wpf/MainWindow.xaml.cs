using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CodeTyper.Wpf.Models;
using CodeTyper.Wpf.Services;

namespace CodeTyper.Wpf;

public partial class MainWindow : Window
{
    private readonly ApiClient _api;
    private List<ModeDefinition> _modes = [];
    private ModeDefinition? _selectedMode;

    // プレイヤー情報（ホーム画面から受け取る）
    private readonly string? _playerName;
    private readonly string _scope;
    private readonly string? _groupId;

    // ── Game state ────────────────────────────────────────────────────────────
    private List<WordEntry> _words = [];
    private int _currentIndex = 0;
    private int _correctCount = 0;
    private int _missCount = 0;
    private int _totalTyped = 0;
    private double _liveScore = 0;
    private DateTime _startedAt;
    private DispatcherTimer? _timer;
    private bool _gameRunning = false;
    private bool _currentWordHasMistake = false;
    private bool _isAdvancingWord = false;
    private bool _isEndingGame = false;
    private bool _adminUnlocked = false;
    private bool _handlingTabSelectionChange = false;
    private int _lastNonAdminTabIndex = 0;

    private const string BaseUrl = "http://localhost:5000";
    private const int FixedWordCount = 20;
    private const int TimeLimitSeconds = 60;
    private const int AdminTabIndex = 2;

    public MainWindow(string? playerName, string scope, string? groupId, bool openRanking = false)
    {
        InitializeComponent();
        _playerName = playerName;
        _scope = scope;
        _groupId = groupId;

        var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _api = new ApiClient(http);
        Loaded += async (_, _) =>
        {
            await InitAsync();
            if (openRanking)
                MainTabControl.SelectedIndex = 1;
        };
    }

    private async Task InitAsync()
    {
        try
        {
            _modes = await _api.GetModesAsync();
            PopulateModeGrid();
            PopulateRankingFilters();
            PopulateAdminFilters();

            // ヘッダーにプレイヤー情報を表示
            if (_playerName is not null)
            {
                var scopeLabel = _scope == "team" ? $"Group: {_groupId}" : "Global";
                StatusText.Text = $"Player: {_playerName}  |  {scopeLabel}  |  API Connected";
            }
            else
            {
                StatusText.Text = "API Connected";
            }

            // ゲーム画面のスコープ選択は不要（ホームで決定済み）
            ScopePanel.Visibility = _playerName is not null ? Visibility.Collapsed : Visibility.Visible;
            PlayerNameBox.Text = _playerName ?? "";
            PlayerNameBox.IsEnabled = _playerName is null;
            LiveTime.Text = TimeLimitSeconds.ToString();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"API Error: {ex.Message}";
            MessageBox.Show($"Cannot connect to API server.\n{BaseUrl}\n\n{ex.Message}",
                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Mode ──────────────────────────────────────────────────────────────────
    private void PopulateModeGrid()
    {
        var langs = _modes.Select(m => m.Language).Distinct().OrderBy(x => x).ToList();
        var diffs = _modes.Select(m => m.Difficulty).Distinct().OrderBy(x => x).ToList();

        LangCombo.ItemsSource = langs;
        DiffCombo.ItemsSource = diffs;

        if (langs.Count > 0) LangCombo.SelectedIndex = 0;
        if (diffs.Count > 0) DiffCombo.SelectedIndex = 0;
    }

    private void LangCombo_Changed(object sender, SelectionChangedEventArgs e) => UpdateSelectedMode();

    private void DiffCombo_Changed(object sender, SelectionChangedEventArgs e) => UpdateSelectedMode();

    private void UpdateSelectedMode()
    {
        var lang = LangCombo.SelectedItem?.ToString();
        var diff = DiffCombo.SelectedItem?.ToString();
        if (lang is null || diff is null) return;

        _selectedMode = _modes.FirstOrDefault(m => m.Language == lang && m.Difficulty == diff);
    }

    private void ScopeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TeamIdPanel is null) return;
        TeamIdPanel.Visibility = GetTag(ScopeCombo) == "team" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Game Start ────────────────────────────────────────────────────────────
    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectedMode();
        if (_selectedMode is null)
        {
            MessageBox.Show("Please select a mode.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var playerName = _playerName ?? PlayerNameBox.Text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            MessageBox.Show("Please enter your player name.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _words = await _api.GetWordsAsync(_selectedMode.Language, _selectedMode.Difficulty, FixedWordCount);
            if (_words.Count == 0)
            {
                MessageBox.Show("No words found for this mode.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load words: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _currentIndex = 0;
        _correctCount = 0;
        _missCount = 0;
        _totalTyped = 0;
        _liveScore = 0;
        _gameRunning = true;
        _currentWordHasMistake = false;
        _isAdvancingWord = false;
        _isEndingGame = false;
        _startedAt = DateTime.Now;

        ResultCard.Visibility = Visibility.Collapsed;
        StartBtn.Visibility = Visibility.Collapsed;
        RetryBtn.Visibility = Visibility.Collapsed;
        InputBox.IsEnabled = true;
        PlayerNameBox.IsEnabled = false;
        LiveTime.Text = TimeLimitSeconds.ToString();

        UpdateProgressBar();
        ShowCurrentWord();
        StartTimer();
        InputBox.Focus();
    }

    // ── Word Display ──────────────────────────────────────────────────────────
    private void ShowCurrentWord()
    {
        if (!_gameRunning || _isEndingGame)
            return;

        if (_currentIndex >= _words.Count)
        {
            EndGame();
            return;
        }

        _currentWordHasMistake = false;
        _isAdvancingWord = false;
        CurrentWordText.Text = _words[_currentIndex].Word;
        CurrentWordText.Foreground = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0));

        NextWordText.Text = _currentIndex + 1 < _words.Count
            ? $"Next: {_words[_currentIndex + 1].Word}"
            : "Last word!";

        InputBox.IsEnabled = true;
        InputBox.Text = "";
        InputBox.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x36));
        FeedbackText.Text = "";
        FeedbackText.Foreground = Brushes.Transparent;
        InputBox.Focus();
    }

    // ── Input Handling ────────────────────────────────────────────────────────
    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_gameRunning || _isAdvancingWord || _isEndingGame) return;

        var input = InputBox.Text;
        var target = _words[_currentIndex].Word;
        var hasError = input.Length > 0 && !target.StartsWith(input, StringComparison.Ordinal);

        if (hasError && !_currentWordHasMistake)
        {
            _currentWordHasMistake = true;
            _liveScore = 0;
            ShowFeedback("Score reset", isCorrect: false);
        }

        InputBox.Background = hasError
            ? new SolidColorBrush(Color.FromRgb(0x3b, 0x1a, 0x1a))
            : new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x36));

        UpdateLiveStats();

        if (input.Length >= target.Length)
            ResolveCurrentWord(input, target);
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_gameRunning) return;
        if (e.Key == System.Windows.Input.Key.Space || e.Key == System.Windows.Input.Key.Enter)
            e.Handled = true;
    }

    private void ResolveCurrentWord(string input, string target)
    {
        if (_isAdvancingWord || _isEndingGame) return;

        _isAdvancingWord = true;
        InputBox.IsEnabled = false;

        if (!_currentWordHasMistake && string.Equals(input, target, StringComparison.Ordinal))
        {
            _correctCount++;
            _totalTyped += target.Length;

            var wordScore = target.Length * DifficultyMultiplier(_selectedMode!.Difficulty);
            _liveScore += wordScore;

            CurrentWordText.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            ShowFeedback($"+{(int)wordScore}", isCorrect: true);
        }
        else
        {
            _missCount++;
            _liveScore = 0;
            CurrentWordText.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            ShowFeedback("Score reset", isCorrect: false);
        }

        LiveScore.Text = ((int)_liveScore).ToString();
        _currentIndex++;
        UpdateProgressBar();

        var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            ShowCurrentWord();
        };
        delay.Start();
    }

    private static double DifficultyMultiplier(string difficulty) => difficulty switch
    {
        "easy" => 1.0,
        "normal" => 1.5,
        "hard" => 2.0,
        _ => 1.0
    };

    private void ShowFeedback(string text, bool isCorrect)
    {
        FeedbackText.Text = text;
        FeedbackText.Foreground = isCorrect
            ? new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e))
            : new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
    }

    // ── Progress ──────────────────────────────────────────────────────────────
    private void UpdateProgressBar()
    {
        if (_words.Count == 0) return;
        ProgressText.Text = $"{_currentIndex} / {_words.Count}";

        var parent = ProgressBar.Parent as Border;
        if (parent is null) return;
        double ratio = (double)_currentIndex / _words.Count;
        ProgressBar.Width = Math.Max(0, parent.ActualWidth * ratio);
    }

    // ── Timer & Live Stats ────────────────────────────────────────────────────
    private void StartTimer()
    {
        _timer?.Stop();
        LiveTime.Text = TimeLimitSeconds.ToString();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) =>
        {
            var remaining = GetRemainingSeconds();
            LiveTime.Text = remaining.ToString();
            UpdateLiveStats();

            if (remaining <= 0)
                EndGame();
        };
        _timer.Start();
    }

    private void UpdateLiveStats()
    {
        var elapsed = Math.Min((DateTime.Now - _startedAt).TotalSeconds, TimeLimitSeconds);
        elapsed = Math.Max(elapsed, 1);
        var wpm = (_totalTyped / 5.0) / (elapsed / 60.0);
        var totalAttempts = _correctCount + _missCount;
        var acc = totalAttempts == 0 ? 100.0 : (double)_correctCount / totalAttempts * 100;

        LiveWpm.Text = ((int)wpm).ToString();
        LiveAcc.Text = ((int)acc).ToString();
        LiveScore.Text = ((int)_liveScore).ToString();
    }

    // ── Game End ──────────────────────────────────────────────────────────────
    private async void EndGame()
    {
        if (_isEndingGame) return;

        _isEndingGame = true;
        _gameRunning = false;
        _timer?.Stop();
        InputBox.IsEnabled = false;
        PlayerNameBox.IsEnabled = true;

        var elapsed = Math.Min((DateTime.Now - _startedAt).TotalSeconds, TimeLimitSeconds);
        elapsed = Math.Max(elapsed, 1);
        var finishedAllWords = _currentIndex >= _words.Count;
        var remainingBonus = GetRemainingSeconds();
        var finalScore = _liveScore + remainingBonus;
        var wpm = (_totalTyped / 5.0) / (elapsed / 60.0);
        var totalAttempts = _correctCount + _missCount;
        var acc = totalAttempts == 0 ? 100.0 : (double)_correctCount / totalAttempts * 100;

        ResScore.Text = ((int)finalScore).ToString();
        ResWpm.Text = wpm.ToString("F1");
        ResAcc.Text = $"{acc:F1}%";
        ResCorrect.Text = $"{_correctCount}/{_words.Count}";
        ResultCard.Visibility = Visibility.Visible;
        RetryBtn.Visibility = Visibility.Visible;
        ViewRankingBtn.Visibility = Visibility.Visible;

        CurrentWordText.Text = finishedAllWords ? "Finished!" : "Time Up!";
        NextWordText.Text = "";
        FeedbackText.Text = remainingBonus > 0
            ? $"+{remainingBonus} time bonus"
            : "";

        var playerName = _playerName ?? PlayerNameBox.Text.Trim();
        var scope = _playerName is not null ? _scope : GetTag(ScopeCombo) ?? "global";
        var teamId = scope == "team" ? (_groupId ?? TeamIdBox.Text.Trim()) : null;

        var userId = $"player:{playerName.ToLower().Replace(" ", "_")}";
        try
        {
            await _api.UpsertUserAsync(userId, playerName, teamId);
        }
        catch
        {
        }

        var submission = new ScoreSubmission(
            userId, playerName, teamId, scope,
            _selectedMode!.Language, _selectedMode.Difficulty,
            _totalTyped,
            Math.Round(wpm, 2),
            Math.Round(acc, 2),
            _missCount,
            Math.Round(finalScore, 2));

        try
        {
            var saved = await _api.SubmitScoreAsync(submission);
            if (saved is not null)
            {
                ResScore.Text = ((int)saved.Score).ToString();
                LiveScore.Text = ((int)saved.Score).ToString();
            }
            StatusText.Text = "Score saved!";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Score save failed: {ex.Message}";
        }
    }

    private void RetryBtn_Click(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _gameRunning = false;
        _currentWordHasMistake = false;
        _isAdvancingWord = false;
        _isEndingGame = false;
        ResultCard.Visibility = Visibility.Collapsed;
        RetryBtn.Visibility = Visibility.Collapsed;
        ViewRankingBtn.Visibility = Visibility.Collapsed;
        StartBtn.Visibility = Visibility.Visible;
        InputBox.IsEnabled = false;
        InputBox.Text = "";
        InputBox.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x36));
        CurrentWordText.Text = "---";
        NextWordText.Text = "";
        FeedbackText.Text = "";
        LiveScore.Text = "0";
        LiveWpm.Text = "0";
        LiveAcc.Text = "100";
        LiveTime.Text = TimeLimitSeconds.ToString();
        ProgressText.Text = "0 / 0";
        ProgressBar.Width = 0;
    }

    private int GetRemainingSeconds()
    {
        var elapsed = (DateTime.Now - _startedAt).TotalSeconds;
        return Math.Max(0, TimeLimitSeconds - (int)Math.Ceiling(elapsed));
    }

    private void ViewRanking_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMode is not null)
        {
            var langs = RankLangCombo.ItemsSource as List<string>;
            var diffs = RankDiffCombo.ItemsSource as List<string>;
            var li = langs?.IndexOf(_selectedMode.Language) ?? -1;
            var di = diffs?.IndexOf(_selectedMode.Difficulty) ?? -1;
            if (li >= 0) RankLangCombo.SelectedIndex = li;
            if (di >= 0) RankDiffCombo.SelectedIndex = di;

            var scope = _playerName is not null ? _scope : GetTag(ScopeCombo) ?? "global";
            for (int i = 0; i < RankScopeCombo.Items.Count; i++)
            {
                if ((RankScopeCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == scope)
                {
                    RankScopeCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        MainTabControl.SelectedIndex = 1;
        LoadRanking_Click(sender, e);
    }

    private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, MainTabControl) || _handlingTabSelectionChange)
            return;

        if (MainTabControl.SelectedIndex == AdminTabIndex)
        {
            if (_adminUnlocked)
                return;

            var password = PromptForAdminPassword();
            if (string.IsNullOrEmpty(password))
            {
                ReturnToLastNonAdminTab();
                return;
            }

            try
            {
                if (!await _api.AdminLoginAsync(password))
                {
                    MessageBox.Show("Incorrect password.", "Access denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ReturnToLastNonAdminTab();
                    return;
                }

                await LoadAdminWordsAsync();
                _adminUnlocked = true;
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Word load error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ReturnToLastNonAdminTab();
            return;
        }

        _lastNonAdminTabIndex = MainTabControl.SelectedIndex;
    }

    // ── Ranking ───────────────────────────────────────────────────────────────
    private void PopulateRankingFilters()
    {
        var langs = _modes.Select(m => m.Language).Distinct().ToList();
        var diffs = _modes.Select(m => m.Difficulty).Distinct().ToList();
        RankLangCombo.ItemsSource = langs;
        RankLangCombo.SelectedIndex = 0;
        RankDiffCombo.ItemsSource = diffs;
        RankDiffCombo.SelectedIndex = 0;
    }

    private void RankScopeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (RankTeamPanel is null) return;
        RankTeamPanel.Visibility = GetTag(RankScopeCombo) == "team" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void LoadRanking_Click(object sender, RoutedEventArgs e)
    {
        var lang = RankLangCombo.SelectedItem?.ToString() ?? "java";
        var diff = RankDiffCombo.SelectedItem?.ToString() ?? "easy";
        var scope = GetTag(RankScopeCombo) ?? "global";
        var teamId = RankTeamBox.Text;
        var top = int.Parse((RankTopCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "20");

        try
        {
            var rows = await _api.GetRankingAsync(scope, lang, diff, teamId, top);
            RankingGrid.ItemsSource = rows.Select((r, i) => new
            {
                Rank = i + 1,
                r.DisplayName,
                Score = ((int)r.Score).ToString(),
                Wpm = r.Wpm.ToString("F1"),
                Accuracy = $"{r.Accuracy:F1}%",
                PlayedAt = r.PlayedAt.LocalDateTime.ToString("yyyy/MM/dd HH:mm")
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ranking load error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Admin ─────────────────────────────────────────────────────────────────
    private void PopulateAdminFilters()
    {
        var langs = new List<string> { "(All)" }.Concat(_modes.Select(m => m.Language).Distinct()).ToList();
        var diffs = new List<string> { "(All)" }.Concat(_modes.Select(m => m.Difficulty).Distinct()).ToList();
        AdminLangCombo.ItemsSource = langs;
        AdminLangCombo.SelectedIndex = 0;
        AdminDiffCombo.ItemsSource = diffs;
        AdminDiffCombo.SelectedIndex = 0;
    }

    private async void SearchWords_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadAdminWordsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Word load error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadAdminWordsAsync()
    {
        var lang = AdminLangCombo.SelectedItem?.ToString();
        var diff = AdminDiffCombo.SelectedItem?.ToString();
        if (lang == "(All)") lang = null;
        if (diff == "(All)") diff = null;

        var words = await _api.GetAllWordsAsync(lang, diff);
        WordGrid.ItemsSource = words.Select(w => new WordRow(w)).ToList();
    }

    private void ReturnToLastNonAdminTab()
    {
        _handlingTabSelectionChange = true;
        MainTabControl.SelectedIndex = _lastNonAdminTabIndex;
        _handlingTabSelectionChange = false;
    }

    private void AddWord_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new WordEditDialog(null);
        if (dlg.ShowDialog() == true) _ = SaveNewWord(dlg.Result!);
    }

    private async Task SaveNewWord(WordUpsertRequest req)
    {
        try
        {
            await _api.AddWordAsync(req);
            SearchWords_Click(this, new RoutedEventArgs());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Add error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditWord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WordRow row)
        {
            var dlg = new WordEditDialog(row.Source);
            if (dlg.ShowDialog() == true) _ = UpdateWord(row.Source.WordId, dlg.Result!);
        }
    }

    private async Task UpdateWord(Guid wordId, WordUpsertRequest req)
    {
        try
        {
            await _api.UpdateWordAsync(wordId, req);
            SearchWords_Click(this, new RoutedEventArgs());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteWord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WordRow row)
        {
            if (MessageBox.Show($"Delete \"{row.Word}\"?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _ = DeleteWord(row.Source.WordId);
            }
        }
    }

    private async Task DeleteWord(Guid wordId)
    {
        try
        {
            await _api.DeleteWordAsync(wordId);
            SearchWords_Click(this, new RoutedEventArgs());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string? GetTag(ComboBox combo) =>
        (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private void HomeBtn_Click(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        new HomeWindow().Show();
        Close();
    }

    private string? PromptForAdminPassword()
    {
        var dialog = new Window
        {
            Title = "Admin Password",
            Width = 320,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Owner = this,
            Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1d, 0x27)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0))
        };

        var passwordBox = new PasswordBox
        {
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(8, 6, 8, 6)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 90,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xf1)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x36)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(0)
        };

        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);

        var root = new StackPanel
        {
            Margin = new Thickness(20)
        };
        root.Children.Add(new TextBlock
        {
            Text = "Enter the admin password to open Word Admin.",
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        });
        root.Children.Add(passwordBox);
        root.Children.Add(buttons);

        dialog.Content = root;
        dialog.Loaded += (_, _) => passwordBox.Focus();
        passwordBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                dialog.DialogResult = true;
        };

        return dialog.ShowDialog() == true ? passwordBox.Password : null;
    }
}

public class WordRow(WordEntry src)
{
    public WordEntry Source { get; } = src;
    public string Word => Source.Word;
    public string Language => Source.Language;
    public string Difficulty => Source.Difficulty;
    public int Weight => Source.Weight;
    public string EnabledText => Source.Enabled ? "Yes" : "No";
}
