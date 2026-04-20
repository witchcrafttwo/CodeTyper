let startedAt = null;
let cachedWords = [];

const userId = document.getElementById('userId');
const displayName = document.getElementById('displayName');
const teamId = document.getElementById('teamId');
const globalAlias = document.getElementById('globalAlias');
const language = document.getElementById('language');
const difficulty = document.getElementById('difficulty');
const scope = document.getElementById('scope');
const targetText = document.getElementById('targetText');
const inputText = document.getElementById('inputText');
const result = document.getElementById('result');
const ranking = document.getElementById('ranking');

async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { 'Content-Type': 'application/json' },
    ...options
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

async function loadModes() {
  const modes = await api('/modes');
  const langs = [...new Set(modes.map(m => m.language))];
  const diffs = [...new Set(modes.map(m => m.difficulty))];

  language.innerHTML = langs.map(v => `<option>${v}</option>`).join('');
  difficulty.innerHTML = diffs.map(v => `<option>${v}</option>`).join('');
}

async function saveUser() {
  await api('/users/upsert', {
    method: 'POST',
    body: JSON.stringify({
      userId: userId.value,
      email: `${userId.value}@example.com`,
      displayName: displayName.value,
      teamId: teamId.value,
      globalAlias: globalAlias.value
    })
  });
  alert('ユーザーを保存しました');
}

async function startPractice() {
  const words = await api(`/words?language=${language.value}&difficulty=${difficulty.value}&count=20`);
  cachedWords = words;
  targetText.textContent = words.map(w => w.word).join(' ');
  inputText.value = '';
  result.textContent = '';
  startedAt = performance.now();
}

function calcMetrics() {
  const target = targetText.textContent || '';
  const input = inputText.value;
  const elapsedSec = Math.max((performance.now() - startedAt) / 1000, 1);
  const correctChars = [...input].filter((ch, i) => target[i] === ch).length;
  const missCount = Math.max(input.length - correctChars, 0);
  const accuracy = input.length === 0 ? 0 : (correctChars / input.length) * 100;
  const wpm = (input.length / 5) / (elapsedSec / 60);
  return { correctChars, missCount, accuracy, wpm };
}

async function finishPractice() {
  if (!startedAt) {
    alert('先に練習開始してください');
    return;
  }

  const metrics = calcMetrics();
  const payload = {
    userId: userId.value,
    displayName: displayName.value,
    teamId: teamId.value,
    scope: scope.value,
    language: language.value,
    difficulty: difficulty.value,
    correctChars: Math.round(metrics.correctChars),
    wpm: Number(metrics.wpm.toFixed(2)),
    accuracy: Number(metrics.accuracy.toFixed(2)),
    missCount: Math.round(metrics.missCount)
  };

  const saved = await api('/scores', { method: 'POST', body: JSON.stringify(payload) });
  result.textContent = `score=${saved.score} / wpm=${saved.wpm} / acc=${saved.accuracy}%`;
  await loadRanking();
}

async function loadRanking() {
  const params = new URLSearchParams({
    scope: scope.value,
    language: language.value,
    difficulty: difficulty.value,
    top: '10'
  });

  if (scope.value === 'team') {
    params.set('teamId', teamId.value);
  }

  const rows = await api(`/rankings?${params.toString()}`);
  ranking.innerHTML = rows
    .map(r => `<li>${r.displayName} - ${r.score} (${r.wpm}wpm / ${r.accuracy}%)</li>`)
    .join('');
}

document.getElementById('saveUser').addEventListener('click', saveUser);
document.getElementById('start').addEventListener('click', startPractice);
document.getElementById('finish').addEventListener('click', finishPractice);
document.getElementById('loadRanking').addEventListener('click', loadRanking);

loadModes().catch(err => {
  result.textContent = `初期化エラー: ${err.message}`;
});
