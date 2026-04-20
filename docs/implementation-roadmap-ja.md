# CodeTyper 実装ロードマップ（PostgreSQL / RDS 前提）

「どんな感じで進めるか」を、実装順に分解した実行計画です。

## フェーズ0: 土台を固定（1日）

1. AWS 側の前提を固定
   - RDS PostgreSQL インスタンス作成
   - DB 名: `codetyper`
   - アプリ用ユーザー: `codetyper_app`
2. 接続情報を Secrets Manager に登録
3. アプリ設定
   - `Database:Provider = Postgres`
   - `ConnectionStrings:Postgres` または `DB_*` 環境変数

**完了条件**
- `/health` が 200 を返す
- アプリ起動時にテーブルが作成される

## フェーズ1: MVP API を固める（2〜3日）

対象 API:
- `GET /modes`
- `GET /words`
- `POST /scores`
- `GET /rankings`

作業:
1. バリデーション追加
   - `scope` は `team/global` のみ許可
   - `difficulty` は `easy/normal/hard` のみ
2. エラーハンドリング統一
   - 400/404/500 のレスポンス形式を統一
3. ログ追加
   - score 登録時に userId/mode/score を構造化ログ出力

**完了条件**
- Postman/HTTP ファイルで主要 API が一通り成功

## フェーズ2: 認証とユーザー管理（2〜4日）

1. Cognito + Google ログイン連携
2. JWT 検証ミドルウェア追加
3. `users` テーブル同期 API 追加
   - 初回ログイン時に upsert
4. チームランキングの認可
   - `teamId` を JWT から決定（リクエスト値を信用しない）

**完了条件**
- ログインユーザーだけが score 登録できる
- team ランキングに他チームのデータが混ざらない

## フェーズ3: ランキング品質改善（2日）

1. インデックス最適化
   - `(scope, language, difficulty, team_id, score DESC, played_at DESC)`
2. 同率時ソートルール固定
   - `score DESC, played_at DESC`
3. 不正対策の最低限
   - 極端な WPM は拒否 / 監査ログへ

**完了条件**
- ランキングの並び順が期待どおり
- 負荷時でも遅延が許容範囲

## フェーズ4: GUI クライアント連携（3〜5日）

1. WPF で画面作成
   - モード選択
   - タイピング画面
   - 結果表示
   - ランキング表示
2. API クライアント層実装
3. エラー時 UI（再試行、トースト）

**完了条件**
- 1プレイ完了 → score 登録 → ランキング反映まで通る

## フェーズ5: 運用準備（2日）

1. CI/CD
   - テスト
   - ビルド
   - デプロイ
2. 監視
   - CloudWatch メトリクス
   - エラーレート / p95 latency
3. バックアップ
   - RDS 自動バックアップ有効化

**完了条件**
- 障害時に原因追跡できる
- 最低限の復旧手順がある

---

## 最初の1週間でやること（具体）

Day 1:
- RDS 構築
- 接続設定

Day 2-3:
- API バリデーションとエラー整備

Day 4-5:
- Cognito + Google ログイン
- JWT 検証

Day 6-7:
- WPF 側から API 接続
- MVP の E2E 動作確認

---

## 優先順位（迷った時）

1. 認証（誰のスコアか）
2. スコア保存の整合性
3. ランキングの正しさ
4. UI/UX 改善

この順で進めると、作り直しが最小になります。
