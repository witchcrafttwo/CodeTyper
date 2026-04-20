# CodeTyper

C# で作る「コード頻出単語」向けタイピングアプリの API 実装です。

## 含まれるもの
- ASP.NET Core Minimal API
- **簡易Web GUI（`/`）**
- モード選択（java/python/javascript + easy/normal/hard）
- 単語取得 API
- ユーザー登録/更新 API
- スコア登録 API
- チーム / グローバルランキング API
- **RDB (EF Core)** 保存実装（SQLite / PostgreSQL）

## 起動
```bash
dotnet run --project src/CodeTyper.Api
```

起動後:
- API: `http://localhost:5000`
- GUI: `http://localhost:5000/`

## PostgreSQL 接続設定（RDS PostgreSQL）

### 1) appsettings.json で指定
`src/CodeTyper.Api/appsettings.json`

```json
{
  "Database": { "Provider": "Postgres" },
  "ConnectionStrings": {
    "Postgres": "Host=<endpoint>;Port=5432;Database=codetyper;Username=codetyper_app;Password=<password>;Ssl Mode=Require;Trust Server Certificate=false"
  }
}
```

### 2) 環境変数で指定（推奨）
`ConnectionStrings:Postgres` を空にして、以下を設定してもOKです。

- `DB_HOST`
- `DB_PORT` (default: 5432)
- `DB_NAME`
- `DB_USER`
- `DB_PASSWORD`
- `DB_SSLMODE` (default: Require)
- `DB_TRUST_SERVER_CERT` (default: false)

サンプル: `.env.postgres.example`

## 主要 API
- `GET /health`
- `GET /modes`
- `GET /words?language=java&difficulty=easy&count=20`
- `POST /users/upsert`
- `GET /users/{userId}`
- `PATCH /users/{userId}/alias`
- `POST /scores`
- `GET /rankings?scope=team&language=java&difficulty=easy&teamId=team-a&top=20`

詳細は `docs/database-design-ja.md` と `src/CodeTyper.Api/CodeTyper.Api.http` を参照。

## DB選定（AWS）
- 採用: **RDS PostgreSQL**（詳細は `docs/aws-rdb-choice-ja.md`）

## 実装の進め方
- 実装ロードマップ: `docs/implementation-roadmap-ja.md`
