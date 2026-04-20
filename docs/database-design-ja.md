# CodeTyper 向け AWS データベース設計（RDB / PostgreSQL）

要件に合わせて **NoSQL ではなく RDB** で設計します。AWS では **Amazon RDS for PostgreSQL** を前提にします。

## 1. テーブル設計

## 1.1 `users`
ユーザー情報（Google ログイン主体）

- **PK**: `user_id` (Cognito `sub`)
- カラム
  - `email` varchar(256)
  - `display_name` varchar(64)
  - `team_id` varchar(64)
  - `global_alias` varchar(64)
  - `created_at` timestamptz

## 1.2 `words`
単語マスタ

- **PK**: `word_id` uuid
- カラム
  - `word` varchar(128)
  - `language` varchar(32)
  - `difficulty` varchar(16)
  - `weight` int
  - `enabled` boolean
- インデックス
  - `(language, difficulty)`

## 1.3 `scores`
スコア履歴とランキング

- **PK**: `score_id` varchar(64)
- **FK**: `user_id` -> `users(user_id)`
- カラム
  - `display_name` varchar(64)
  - `team_id` varchar(64)
  - `scope` varchar(16) (`team` / `global`)
  - `language` varchar(32)
  - `difficulty` varchar(16)
  - `wpm` numeric(8,2)
  - `accuracy` numeric(5,2)
  - `score` numeric(10,2)
  - `played_at` timestamptz
- 推奨インデックス
  - `(scope, language, difficulty, team_id, score desc, played_at desc)`

## 2. ランキング SQL

### チーム内ランキング
```sql
SELECT *
FROM scores
WHERE scope = 'team'
  AND language = $1
  AND difficulty = $2
  AND team_id = $3
ORDER BY score DESC, played_at DESC
LIMIT $4;
```

### グローバルランキング
```sql
SELECT *
FROM scores
WHERE scope = 'global'
  AND language = $1
  AND difficulty = $2
ORDER BY score DESC, played_at DESC
LIMIT $3;
```

## 3. Google ログイン連携

1. Amazon Cognito User Pool 作成
2. Google IdP を追加
3. Hosted UI ログイン
4. JWT の `sub` を `users.user_id` として利用

## 4. AWS 構成（RDB）

- DB: Amazon RDS for PostgreSQL
- API: Lambda(.NET) or ECS + ASP.NET Core
- 接続管理: RDS Proxy 推奨
- シークレット: AWS Secrets Manager

## 5. このリポジトリ実装との対応

- EF Core `AppDbContext` で `users/words/scores` をマッピング
- `EfWordStore` と `EfScoreStore` が RDB を使用
- `Database:Provider` で `Sqlite` / `Postgres` を切替

## 6. 接続設定（PostgreSQL）

### appsettings 方式
- `Database:Provider = Postgres`
- `ConnectionStrings:Postgres = Host=...;Port=5432;Database=...;Username=...;Password=...;Ssl Mode=Require;Trust Server Certificate=false`

### 環境変数方式
`ConnectionStrings:Postgres` を設定しない場合は、以下で組み立て可能です。

- `DB_HOST`
- `DB_PORT`（省略時 5432）
- `DB_NAME`
- `DB_USER`
- `DB_PASSWORD`
- `DB_SSLMODE`（省略時 Require）
- `DB_TRUST_SERVER_CERT`（省略時 false）

本番では Secrets Manager でパスワード管理し、アプリ側は環境変数経由で受け取る運用を推奨します。
