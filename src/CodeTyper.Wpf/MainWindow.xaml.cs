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

    private const string BaseUrl = "http://localhost:5000";

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
        }
        catch (Exception ex)
        {
            StatusText.Text = $"API Error: {ex.Message}";
            MessageBox.Show($"Cannot connect to API server.\n{BaseUrl}\n\n{ex.Message}",
                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Mode ──────────────────────────────────────────────────────────────────
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

    private void LangCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedMode();
    }

    private void DiffCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedMode();
    }

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

        var countTag = (WordCountCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "20";
        var count = int.Parse(countTag);

        try
        {
            _words = await _api.GetWordsAsync(_selectedMode.Language, _selectedMode.Difficulty, count);
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

        // Reset state
        _currentIndex = 0;
        _correctCount = 0;
        _missCount = 0;
        _totalTyped = 0;
        _liveScore = 0;
        _gameRunning = true;
        _startedAt = DateTime.Now;

        // UI reset
        ResultCard.Visibility = Visibility.Collapsed;
        StartBtn.Visibility = Visibility.Collapsed;
        RetryBtn.Visibility = Visibility.Collapsed;
        InputBox.IsEnabled = true;
        PlayerNameBox.IsEnabled = false;

        UpdateProgressBar();
        ShowCurrentWord();
        StartTimer();
        InputBox.Focus();
    }

    // ── Word Display ──────────────────────────────────────────────────────────
    private void ShowCurrentWord()
    {
        if (_currentIndex >= _words.Count)
        {
            EndGame();
            return;
        }

        CurrentWordText.Text = _words[_currentIndex].Word;
        CurrentWordText.Foreground = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0));

        // 次の単語プレビュー
        NextWordText.Text = _currentIndex + 1 < _words.Count
            ? $"Next: {_words[_currentIndex + 1].Word}"
            : "Last word!";

        InputBox.Text = "";
        FeedbackText.Text = "";
        FeedbackText.Foreground = Brushes.Transparent;
    }

    // ── Input Handling ────────────────────────────────────────────────────────
    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_gameRunning) return;

        var input = InputBox.Text;
        var target = _words[_currentIndex].Word;

        // 入力中のリアルタイムハイライト
        bool allCorrectSoFar = input.Length > 0 && target.StartsWith(input);
        bool hasError = input.Length > 0 && !target.StartsWith(input);

        InputBox.Background = hasError
            ? new SolidColorBrush(Color.FromRgb(0x3b, 0x1a, 0x1a))
            : new SolidColorBrush(Color.FromRgb(0x25, 0x28, 0x36));

        UpdateLiveStats();
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_gameRunning) return;
        if (e.Key != System.Windows.Input.Key.Space && e.Key != System.Windows.Input.Key.Enter) return;

        e.Handled = true;
        var input = InputBox.Text.Trim();
        var target = _words[_currentIndex].Word;

        if (string.IsNullOrEmpty(input)) return;

        if (input == target)
        {
            // 正解
            _correctCount++;
            _totalTyped += target.Length;

            // スコア加算: 単語の長さ × 難易度ボーナス
            double wordScore = target.Length * DifficultyMultiplier(_selectedMode!.Difficulty);
            _liveScore += wordScore;

            CurrentWordText.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            ShowFeedback($"+{(int)wordScore}", isCorrect: true);
        }
        else
        {
            // 不正解
            _missCount++;
            _liveScore = Math.Max(0, _liveScore - 5);
            CurrentWordText.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            ShowFeedback("-5", isCorrect: false);
        }

        LiveScore.Text = ((int)_liveScore).ToString();
        _currentIndex++;
        UpdateProgressBar();

        // 少し待ってから次の単語へ
        var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        delay.Tick += (_, _) => { delay.Stop(); ShowCurrentWord(); };
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

        // ProgressBarの幅を親に合わせて計算
        var parent = ProgressBar.Parent as Border;
        if (parent is null) return;
        double ratio = (double)_currentIndex / _words.Count;
        ProgressBar.Width = Math.Max(0, parent.ActualWidth * ratio);
    }

    // ── Timer & Live Stats ────────────────────────────────────────────────────
    private void StartTimer()
    {
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.Now - _startedAt).TotalSeconds;
            LiveTime.Text = ((int)elapsed).ToString();
            UpdateLiveStats();
        };
        _timer.Start();
    }

    private void UpdateLiveStats()
    {
        var elapsed = Math.Max((DateTime.Now - _startedAt).TotalSeconds, 1);
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
        _gameRunning = false;
        _timer?.Stop();
        InputBox.IsEnabled = false;
        PlayerNameBox.IsEnabled = true;

        var elapsed = Math.Max((DateTime.Now - _startedAt).TotalSeconds, 1);
        var wpm = (_totalTyped / 5.0) / (elapsed / 60.0);
        var totalAttempts = _correctCount + _missCount;
        var acc = totalAttempts == 0 ? 100.0 : (double)_correctCount / totalAttempts * 100;

        // 結果表示
        ResScore.Text = ((int)_liveScore).ToString();
        ResWpm.Text = wpm.ToString("F1");
        ResAcc.Text = $"{acc:F1}%";
        ResCorrect.Text = $"{_correctCount}/{_words.Count}";
        ResultCard.Visibility = Visibility.Visible;
        RetryBtn.Visibility = Visibility.Visible;
        ViewRankingBtn.Visibility = Visibility.Visible;

        CurrentWordText.Text = "Done!";
        NextWordText.Text = "";
        FeedbackText.Text = "";

        // スコアをDBに送信
        var playerName = _playerName ?? PlayerNameBox.Text.Trim();
        var scope = _playerName is not null ? _scope : GetTag(ScopeCombo) ?? "global";
        var teamId = scope == "team" ? (_groupId ?? TeamIdBox.Text.Trim()) : null;

        // ユーザーをupsert（名前ベースのシンプルなID）
        var userId = $"player:{playerName.ToLower().Replace(" ", "_")}";
        try
        {
            await _api.UpsertUserAsync(userId, playerName, teamId);
        }
        catch { /* ユーザー作成失敗は無視してスコアだけ送る */ }

        var submission = new ScoreSubmission(
            userId, playerName, teamId, scope,
            _selectedMode!.Language, _selectedMode.Difficulty,
            _totalTyped,
            Math.Round(wpm, 2),
            Math.Round(acc, 2),
            _missCount);

        try
        {
            await _api.SubmitScoreAsync(submission);
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
        LiveScore.Text = "0"; LiveWpm.Text = "0"; LiveAcc.Text = "100"; LiveTime.Text = "0";
        ProgressText.Text = "0 / 0";
        ProgressBar.Width = 0;
    }

    private void ViewRanking_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMode is not null)
        {
            // ランキングフィルターに直前のモードを反映
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
                { RankScopeCombo.SelectedIndex = i; break; }
            }
        }
        MainTabControl.SelectedIndex = 1;
        LoadRanking_Click(sender, e);
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
        var lang = AdminLangCombo.SelectedItem?.ToString();
        var diff = AdminDiffCombo.SelectedItem?.ToString();
        if (lang == "(All)") lang = null;
        if (diff == "(All)") diff = null;
        try
        {
            var words = await _api.GetAllWordsAsync(lang, diff);
            WordGrid.ItemsSource = words.Select(w => new WordRow(w)).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Word load error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddWord_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new WordEditDialog(null);
        if (dlg.ShowDialog() == true) _ = SaveNewWord(dlg.Result!);
    }

    private async Task SaveNewWord(WordUpsertRequest req)
    {
        try { await _api.AddWordAsync(req); SearchWords_Click(this, new RoutedEventArgs()); }
        catch (Exception ex) { MessageBox.Show($"Add error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
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
        try { await _api.UpdateWordAsync(wordId, req); SearchWords_Click(this, new RoutedEventArgs()); }
        catch (Exception ex) { MessageBox.Show($"Update error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void DeleteWord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WordRow row)
        {
            if (MessageBox.Show($"Delete \"{row.Word}\"?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                _ = DeleteWord(row.Source.WordId);
        }
    }

    private async Task DeleteWord(Guid wordId)
    {
        try { await _api.DeleteWordAsync(wordId); SearchWords_Click(this, new RoutedEventArgs()); }
        catch (Exception ex) { MessageBox.Show($"Delete error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
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
