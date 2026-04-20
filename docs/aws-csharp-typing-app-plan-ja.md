# C# + AWS GUI タイピングアプリ 設計案（RDB版）

## 1. ゴール
- C# で GUI タイピングアプリを作る。
- コードで頻出する単語を練習できる。
- 単語、ユーザー、点数を DB に保存する。
- ランキング（チーム内 / グローバル）を表示する。
- モード選択（言語・難易度）を可能にする。
- Google ログインに対応する。

## 2. 推奨アーキテクチャ（MVP）

### クライアント
- .NET 8 + WPF（Windows）
- 将来は MAUI でクロスプラットフォーム化も可能

### バックエンド（AWS）
- Amazon API Gateway + AWS Lambda (.NET)
- Amazon Cognito（Google IdP連携）
- **Amazon RDS for PostgreSQL（RDB）**
- 接続効率化に RDS Proxy

## 3. RDB モデル

### `users`
- `user_id` (PK / Cognito sub)
- `email`, `display_name`, `team_id`, `global_alias`, `created_at`

### `words`
- `word_id` (PK)
- `word`, `language`, `difficulty`, `weight`, `enabled`

### `scores`
- `score_id` (PK)
- `user_id` (FK)
- `display_name`, `team_id`, `scope`, `language`, `difficulty`
- `wpm`, `accuracy`, `score`, `played_at`

## 4. API 例
- `GET /modes`
- `GET /words?language=java&difficulty=easy`
- `POST /scores`
- `GET /rankings?scope=team&mode=java#easy`

## 5. 主要機能フロー

### 5.1 Google ログイン
1. Cognito Hosted UI を開く
2. Google 認証後 JWT 取得
3. JWT を付けて API 呼び出し

### 5.2 練習
1. 言語・難易度を選択
2. 単語出題
3. 結果を計測（WPM, 正確率）
4. スコア登録

### 5.3 ランキング
- チーム内ランキング
- グローバルランキング（表示名は `global_alias`）

## 6. スコア計算例
- `score = correctChars + (wpm * 2) + (accuracy * 1.5) - (missCount * 3)`

## 7. セキュリティ
- JWT 検証必須
- team ランキングは JWT の team_id で制限
- グローバル表示名の禁止語フィルタ

## 8. 開発順序
1. Cognito + Google ログイン
2. RDS(PostgreSQL) + API 実装
3. WPF クライアント実装
4. ランキング最適化（INDEX, キャッシュ）
