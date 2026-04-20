# Visual Studio 起動エラー対処

対象エラー:

> プログラム `...\CodeTyper.Api.exe` を開始できません。指定されたファイルが見つかりません。

このエラーは、**実行ファイルがまだビルドされていない**か、**起動構成がずれている**ときに出ます。

## まず最短で直す手順（推奨）

1. **スタートアッププロジェクトを `CodeTyper.Api` に設定**
   - ソリューションエクスプローラーで `CodeTyper.Api` を右クリック
   - 「スタートアップ プロジェクトに設定」

2. **構成を `Debug / Any CPU` にする**
   - 上部ツールバーの構成で `Release` ではなく `Debug` を選択

3. **クリーンして再ビルド**
   - `ビルド > ソリューションのクリーン`
   - `ビルド > ソリューションのリビルド`

4. **`F5` ではなく `Ctrl+F5` で一度起動**
   - デバッグアタッチなしでまず exe 生成を確認

## それでもダメな場合

### A. bin/obj を削除して再生成
- `src/CodeTyper.Api/bin`
- `src/CodeTyper.Api/obj`

を削除してから再ビルド。

### B. SDK を確認
Visual Studio の「ターミナル」で:

```powershell
dotnet --info
```

- .NET 8 SDK が表示されることを確認
- 表示されない場合は .NET 8 SDK をインストール

### C. 起動プロファイルを `CodeTyper.Api` にする
本リポジトリには `Properties/launchSettings.json` を用意しています。

- `commandName: Project`
- `applicationUrl: http://localhost:5000`

になっているか確認。

## コマンドラインでの切り分け

Visual Studio 外で以下が通れば、VS 側設定の問題です。

```powershell
dotnet restore
dotnet build .\CodeTyper.sln
dotnet run --project .\src\CodeTyper.Api
```

## よくある原因まとめ

- Startup Project が別プロジェクトになっている
- Release 構成で古い出力先を見に行っている
- ビルド失敗して exe が生成されていない
- セキュリティソフトが出力ファイルを隔離した

