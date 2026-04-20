# CodeTyper

C# で作る「コード頻出単語」向けタイピングアプリの API 実装です。

## 含まれるもの
- ASP.NET Core Minimal API
- モード選択（java/python/javascript + easy/normal/hard）
- 単語取得 API
- スコア登録 API
- チーム / グローバルランキング API
- **RDB (EF Core)** 保存実装（SQLite / PostgreSQL）

## 起動
```bash
dotnet run --project src/CodeTyper.Api
```

## DB 設定
`src/CodeTyper.Api/appsettings.json`

- `Database:Provider = Sqlite`（ローカル開発向け）
- `Database:Provider = Postgres`（AWS RDS for PostgreSQL 向け）

起動時に `EnsureCreated()` でテーブルを自動作成し、初期単語をシードします。

## 主要 API
- `GET /modes`
- `GET /words?language=java&difficulty=easy&count=20`
- `POST /scores`
- `GET /rankings?scope=team&language=java&difficulty=easy&teamId=team-a&top=20`

詳細は `docs/database-design-ja.md` と `src/CodeTyper.Api/CodeTyper.Api.http` を参照。
