# AWSでのRDB選定（CodeTyper向け）

最終結論（2026-04-20 時点）:

- **採用: Amazon RDS for PostgreSQL**
- SQLは **PostgreSQL**

## 1) なぜRDS PostgreSQLにするか

- 構成がシンプルで学習コストが低い
- C# / EF Core との相性が良い
- スモールスタートしやすく、運用実績も多い
- ランキング用途で必要なインデックス/集計を素直に実装できる

## 2) SQLエンジンの選定

### 推奨: PostgreSQL
- JSONB や拡張機能が使え、将来拡張しやすい
- EF Core + Npgsql で実装しやすい
- チーム内/グローバルランキングのクエリが書きやすい

## 3) インスタンス方針（MVP）

- 初期: `db.t4g.small` など小さめで開始
- ストレージ: gp3
- バックアップ: 自動バックアップ有効化
- 高可用性が必要になったら Multi-AZ を検討

## 4) 実装上の設定

このリポジトリは PostgreSQL を使える構成です。

- `Database:Provider = Postgres`
- `ConnectionStrings:Postgres` に RDS 接続文字列
- もしくは `DB_HOST/DB_NAME/DB_USER/DB_PASSWORD` など環境変数

## 5) セキュリティ運用

- パスワードは Secrets Manager で管理
- アプリは IAM ロール + 環境変数で受け取る
- パブリック公開せず、VPC 内で閉じる

以上より、今回の方針は **RDS PostgreSQL + EF Core** が最適です。
