// ── State ─────────────────────────────────────────────────────────────────────
let state = {
  user: null,          // { userId, email, name, picture }
  mode: null,          // { language, difficulty }
  words: [],           // WordEntry[]
  startedAt: null,
  timerInterval: null,
  modes: [],
};

// ── API helper ────────────────────────────────────────────────────────────────
async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    ...options,
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `HTTP ${res.status}`);
  }
  if (res.status === 204) return null;
  return res.json();
}

// ── Auth ──────────────────────────────────────────────────────────────────────
function loginWithGoogle() {
  window.location.href = '/auth/login';
}

function logout() {
  window.location.href = '/auth/logout';
}

async function loadAuthState() {
  try {
    const me = await api('/auth/me');
    if (me.authenticated) {
      state.user = me;
      document.getElementById('userInfo').classList.remove('hidden');
      document.getElementById('loginBtn').classList.add('hidden');
      document.getElementById('userAvatar').src = me.picture || '';
      document.getElementById('userName').textContent = me.name || me.email;
    }
  } catch (_) {}
}

// ── Tabs ──────────────────────────────────────────────────────────────────────
document.querySelectorAll('.tab-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.add('hidden'));
    btn.classList.add('active');
    document.getElementById(`tab-${btn.dataset.tab}`).classList.remove('hidden');
  });
});

// ── Modes ─────────────────────────────────────────────────────────────────────
async function loadModes() {
  state.modes = await api('/modes');
  renderModeGrid();
  populateRankingFilters();
  populateAdminFilters();
}

function renderModeGrid() {
  const grid = document.getElementById('modeGrid');
  const langs = [...new Set(state.modes.map(m => m.language))];
  const diffs = [...new Set(state.modes.map(m => m.difficulty))];

  // Header row
  let html = `<div class="mode-cell mode-header"></div>`;
  diffs.forEach(d => { html += `<div class="mode-cell mode-header">${d}</div>`; });

  langs.forEach(lang => {
    html += `<div class="mode-cell mode-lang">${lang}</div>`;
    diffs.forEach(diff => {
      const active = state.mode?.language === lang && state.mode?.difficulty === diff;
      html += `<div class="mode-cell mode-btn ${active ? 'active' : ''}" 
                    onclick="selectMode('${lang}','${diff}')">${lang}/${diff}</div>`;
    });
  });

  grid.innerHTML = html;
}

function selectMode(language, difficulty) {
  state.mode = { language, difficulty };
  renderModeGrid();
}

function populateRankingFilters() {
  const langs = [...new Set(state.modes.map(m => m.language))];
  const diffs = [...new Set(state.modes.map(m => m.difficulty))];
  ['rankLang'].forEach(id => {
    document.getElementById(id).innerHTML = langs.map(l => `<option value="${l}">${l}</option>`).join('');
  });
  ['rankDiff'].forEach(id => {
    document.getElementById(id).innerHTML = diffs.map(d => `<option value="${d}">${d}</option>`).join('');
  });
}

function populateAdminFilters() {
  const langs = [...new Set(state.modes.map(m => m.language))];
  const diffs = [...new Set(state.modes.map(m => m.difficulty))];
  const adminLang = document.getElementById('adminLang');
  const adminDiff = document.getElementById('adminDiff');
  adminLang.innerHTML = `<option value="">全て</option>` + langs.map(l => `<option value="${l}">${l}</option>`).join('');
  adminDiff.innerHTML = `<option value="">全て</option>` + diffs.map(d => `<option value="${d}">${d}</option>`).join('');
}

// ── Scope toggle ──────────────────────────────────────────────────────────────
document.getElementById('scope').addEventListener('change', e => {
  document.getElementById('teamIdLabel').classList.toggle('hidden', e.target.value !== 'team');
});
document.getElementById('rankScope').addEventListener('change', e => {
  document.getElementById('rankTeamLabel').classList.toggle('hidden', e.target.value !== 'team');
});

// ── Practice ──────────────────────────────────────────────────────────────────
async function startPractice() {
  if (!state.mode) { alert('モードを選択してください'); return; }

  const count = parseInt(document.getElementById('wordCount').value, 10);
  state.words = await api(`/words?language=${state.mode.language}&difficulty=${state.mode.difficulty}&count=${count}`);

  renderTargetText();
  const input = document.getElementById('inputText');
  input.value = '';
  input.disabled = false;
  input.focus();

  document.getElementById('resultCard').classList.add('hidden');
  document.getElementById('startBtn').classList.add('hidden');
  document.getElementById('finishBtn').classList.remove('hidden');
  document.getElementById('retryBtn').classList.add('hidden');

  state.startedAt = performance.now();
  startTimer();
}

function renderTargetText() {
  const display = document.getElementById('targetDisplay');
  display.innerHTML = state.words.map(w =>
    `<span class="word" data-word="${w.word}">${[...w.word].map(c => `<span>${c}</span>`).join('')}</span>`
  ).join('<span class="space"> </span>');
}

function startTimer() {
  clearInterval(state.timerInterval);
  state.timerInterval = setInterval(() => {
    const elapsed = (performance.now() - state.startedAt) / 1000;
    document.getElementById('liveTime').textContent = Math.floor(elapsed);
    updateLiveStats();
  }, 500);
}

function updateLiveStats() {
  const metrics = calcMetrics();
  document.getElementById('liveWpm').textContent = Math.round(metrics.wpm);
  document.getElementById('liveAcc').textContent = Math.round(metrics.accuracy);
}

document.getElementById('inputText').addEventListener('input', () => {
  if (!state.startedAt) return;
  highlightText();
  updateLiveStats();
});

function highlightText() {
  const input = document.getElementById('inputText').value;
  const fullTarget = state.words.map(w => w.word).join(' ');
  const spans = document.querySelectorAll('#targetDisplay span:not(.space)');
  let charIdx = 0;

  document.querySelectorAll('#targetDisplay .word').forEach(wordEl => {
    const charSpans = wordEl.querySelectorAll('span');
    charSpans.forEach(span => {
      const ch = input[charIdx];
      if (ch === undefined) {
        span.className = '';
      } else if (ch === span.textContent) {
        span.className = 'correct';
      } else {
        span.className = 'wrong';
      }
      charIdx++;
    });
    charIdx++; // space
  });
}

function calcMetrics() {
  const target = state.words.map(w => w.word).join(' ');
  const input = document.getElementById('inputText').value;
  const elapsedSec = Math.max((performance.now() - state.startedAt) / 1000, 1);
  const correctChars = [...input].filter((ch, i) => target[i] === ch).length;
  const missCount = Math.max(input.length - correctChars, 0);
  const accuracy = input.length === 0 ? 100 : (correctChars / input.length) * 100;
  const wpm = (input.length / 5) / (elapsedSec / 60);
  return { correctChars, missCount, accuracy, wpm };
}

async function finishPractice() {
  if (!state.startedAt) { alert('先に練習開始してください'); return; }
  clearInterval(state.timerInterval);

  const metrics = calcMetrics();
  const userId = state.user?.userId ?? 'demo-user';
  const displayName = state.user?.name ?? 'Guest';
  const scope = document.getElementById('scope').value;
  const teamId = document.getElementById('teamId').value || null;

  const payload = {
    userId,
    displayName,
    teamId: scope === 'team' ? teamId : null,
    scope,
    language: state.mode.language,
    difficulty: state.mode.difficulty,
    correctChars: Math.round(metrics.correctChars),
    wpm: parseFloat(metrics.wpm.toFixed(2)),
    accuracy: parseFloat(metrics.accuracy.toFixed(2)),
    missCount: Math.round(metrics.missCount),
  };

  try {
    const saved = await api('/scores', { method: 'POST', body: JSON.stringify(payload) });
    document.getElementById('resScore').textContent = Math.round(saved.score);
    document.getElementById('resWpm').textContent = saved.wpm;
    document.getElementById('resAcc').textContent = `${saved.accuracy}%`;
    document.getElementById('resultCard').classList.remove('hidden');
  } catch (e) {
    alert(`送信エラー: ${e.message}`);
  }

  document.getElementById('finishBtn').classList.add('hidden');
  document.getElementById('retryBtn').classList.remove('hidden');
  document.getElementById('inputText').disabled = true;
}

function retryPractice() {
  document.getElementById('startBtn').classList.remove('hidden');
  document.getElementById('retryBtn').classList.add('hidden');
  document.getElementById('resultCard').classList.add('hidden');
  document.getElementById('targetDisplay').innerHTML = '<span class="placeholder">「練習開始」を押してください</span>';
  document.getElementById('inputText').value = '';
  document.getElementById('inputText').disabled = true;
  document.getElementById('liveWpm').textContent = '0';
  document.getElementById('liveAcc').textContent = '100';
  document.getElementById('liveTime').textContent = '0';
  state.startedAt = null;
  clearInterval(state.timerInterval);
}

// ── Ranking ───────────────────────────────────────────────────────────────────
async function loadRanking() {
  const scope = document.getElementById('rankScope').value;
  const language = document.getElementById('rankLang').value;
  const difficulty = document.getElementById('rankDiff').value;
  const top = document.getElementById('rankTop').value;
  const teamId = document.getElementById('rankTeamId').value;

  const params = new URLSearchParams({ scope, language, difficulty, top });
  if (scope === 'team' && teamId) params.set('teamId', teamId);

  try {
    const rows = await api(`/rankings?${params}`);
    const table = document.getElementById('rankingTable');
    if (rows.length === 0) {
      table.innerHTML = '<p class="empty-msg">データがありません</p>';
      return;
    }
    table.innerHTML = `
      <table class="ranking-tbl">
        <thead><tr><th>#</th><th>名前</th><th>スコア</th><th>WPM</th><th>精度</th><th>日時</th></tr></thead>
        <tbody>
          ${rows.map((r, i) => `
            <tr class="${i < 3 ? `top${i+1}` : ''}">
              <td class="rank-num">${i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : i + 1}</td>
              <td>${escHtml(r.displayName)}</td>
              <td class="score-val">${Math.round(r.score)}</td>
              <td>${r.wpm}</td>
              <td>${r.accuracy}%</td>
              <td>${new Date(r.playedAt).toLocaleString('ja-JP')}</td>
            </tr>`).join('')}
        </tbody>
      </table>`;
  } catch (e) {
    document.getElementById('rankingTable').innerHTML = `<p class="error-msg">エラー: ${e.message}</p>`;
  }
}

// ── Admin: Word Management ────────────────────────────────────────────────────
async function loadAdminWords() {
  const lang = document.getElementById('adminLang').value;
  const diff = document.getElementById('adminDiff').value;
  const params = new URLSearchParams();
  if (lang) params.set('language', lang);
  if (diff) params.set('difficulty', diff);

  try {
    const words = await api(`/admin/words?${params}`);
    const wrap = document.getElementById('adminWordTable');
    if (words.length === 0) {
      wrap.innerHTML = '<p class="empty-msg">単語がありません</p>';
      return;
    }
    wrap.innerHTML = `
      <table class="admin-tbl">
        <thead><tr><th>単語</th><th>言語</th><th>難易度</th><th>重み</th><th>有効</th><th>操作</th></tr></thead>
        <tbody>
          ${words.map(w => `
            <tr>
              <td><strong>${escHtml(w.word)}</strong></td>
              <td><span class="badge badge-${w.language}">${w.language}</span></td>
              <td><span class="badge badge-${w.difficulty}">${w.difficulty}</span></td>
              <td>${w.weight}</td>
              <td>${w.enabled ? '✅' : '❌'}</td>
              <td>
                <button class="btn btn-sm btn-secondary" onclick='openWordModal(${JSON.stringify(w)})'>編集</button>
                <button class="btn btn-sm btn-danger" onclick="deleteWord('${w.wordId}')">削除</button>
              </td>
            </tr>`).join('')}
        </tbody>
      </table>`;
  } catch (e) {
    document.getElementById('adminWordTable').innerHTML = `<p class="error-msg">エラー: ${e.message}</p>`;
  }
}

function openWordModal(word = null) {
  document.getElementById('modalTitle').textContent = word ? '単語を編集' : '単語を追加';
  document.getElementById('editWordId').value = word?.wordId ?? '';
  document.getElementById('editWord').value = word?.word ?? '';
  document.getElementById('editLang').value = word?.language ?? 'java';
  document.getElementById('editDiff').value = word?.difficulty ?? 'easy';
  document.getElementById('editWeight').value = word?.weight ?? 5;
  document.getElementById('editEnabled').checked = word?.enabled ?? true;
  document.getElementById('wordModal').classList.remove('hidden');
}

function closeWordModal() {
  document.getElementById('wordModal').classList.add('hidden');
}

async function saveWord() {
  const wordId = document.getElementById('editWordId').value;
  const payload = {
    word: document.getElementById('editWord').value.trim(),
    language: document.getElementById('editLang').value,
    difficulty: document.getElementById('editDiff').value,
    weight: parseInt(document.getElementById('editWeight').value, 10),
    enabled: document.getElementById('editEnabled').checked,
  };

  if (!payload.word) { alert('単語を入力してください'); return; }

  try {
    if (wordId) {
      await api(`/admin/words/${wordId}`, { method: 'PUT', body: JSON.stringify(payload) });
    } else {
      await api('/admin/words', { method: 'POST', body: JSON.stringify(payload) });
    }
    closeWordModal();
    loadAdminWords();
  } catch (e) {
    alert(`保存エラー: ${e.message}`);
  }
}

async function deleteWord(wordId) {
  if (!confirm('この単語を削除しますか？')) return;
  try {
    await api(`/admin/words/${wordId}`, { method: 'DELETE' });
    loadAdminWords();
  } catch (e) {
    alert(`削除エラー: ${e.message}`);
  }
}

// ── Utils ─────────────────────────────────────────────────────────────────────
function escHtml(str) {
  return String(str).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

// ── Init ──────────────────────────────────────────────────────────────────────
(async () => {
  await loadAuthState();
  await loadModes();
  // デフォルトモード選択
  if (state.modes.length > 0) {
    selectMode(state.modes[0].language, state.modes[0].difficulty);
  }
})();
