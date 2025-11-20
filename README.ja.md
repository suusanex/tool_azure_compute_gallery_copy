# Azure Compute Gallery クロスサブスクリプションコピーツール

[English](./README.md)

## 概要

**Azure Compute Gallery (ACG) クロスサブスクリプションコピーツール**は、同一Azure ADテナント内の異なるサブスクリプション間で、Azure Compute Galleryのイメージ定義とバージョンをコピーするWindowsベースのコマンドラインツールです。

### 主な機能

- ✅ **一括コピー**: イメージ定義とバージョンをまとめてコピー
- ✅ **冪等性**: 何度実行しても安全 - 既存リソースは自動スキップ
- ✅ **フィルタ機能**: 前方一致/部分一致によるイメージ・バージョン選別
- ✅ **ドライラン**: 実際の変更なしで予定操作を確認
- ✅ **詳細ログ**: Operation IDとエラーコードでトラブルシューティング対応
- ✅ **クロスサブスクリプション**: 同一テナント内の別サブスクリプション間コピー
- ✅ **WebView2認証**: ブラウザに依存しない埋め込み認証

### 制約事項

- ❌ クロステナント操作は非対応
- ❌ CMK（顧客管理キー）暗号化イメージは自動スキップ
- ❌ OS種別・世代・アーキテクチャの属性不一致時はエラー

---

## 前提条件

### 1. 環境要件

- **オペレーティングシステム**: Windows 10/11 または Windows Server 2019/2022（Windows専用）
- **.NET Runtime**: .NET 10 以降
- **WebView2 Runtime**: Microsoft Edge WebView2 Runtime
  - Windows 11には標準搭載
  - Windows 10には別途インストール必要: [WebView2](https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/)
- **ネットワーク**: Azure管理API（ポート443）へのHTTPSアクセス

### 2. Azure要件

- イメージ定義とバージョンを含むソースACG
- ターゲットリソースグループ（ターゲットギャラリーは自動作成可能）
- RBAC: ソースサブスクリプションに`Reader`、ターゲットサブスクリプションに`Contributor`

### 3. Azure ADアプリケーション登録

詳細は[クイックスタートガイド](./specs/001-acg-gallery-copy/quickstart.md#4-azure-ad-appplication-registration)を参照してください。

---

## クイックスタート

### 1. インストール

[Releases](../../releases)から最新バージョンをダウンロード、またはソースからビルドします。

### 2. 設定

`appsettings.json`を作成:

```json
{
  "source": {
    "subscriptionId": "ソースサブスクリプションID",
    "resourceGroupName": "ソースのリソースグループ名",
    "galleryName": "ソースギャラリー名"
  },
  "target": {
    "subscriptionId": "ターゲットサブスクリプションID",
    "resourceGroupName": "ターゲットのリソースグループ名",
    "galleryName": "ターゲットギャラリー名"
  },
  "authentication": {
    "tenantId": "あなたのテナントID",
    "clientId": "アプリ登録のクライアントID"
  },
  "logLevel": "Information"
}
```

### 3. 設定検証

```bash
acg-copy validate --config appsettings.json
```

### 4. ドライランで確認

変更なしで予定操作を確認:

```bash
acg-copy copy --config appsettings.json --dry-run
```

### 5. コピー実行

```bash
acg-copy copy --config appsettings.json
```

---

## コマンド一覧

### `copy` - ギャラリー間のイメージコピー

```bash
acg-copy copy \
  --source-subscription <サブスクリプションID> \
  --source-resource-group <リソースグループ名> \
  --source-gallery <ギャラリー名> \
  --target-subscription <サブスクリプションID> \
  --target-resource-group <リソースグループ名> \
  --target-gallery <ギャラリー名> \
  --tenant-id <テナントID> \
  [--include-images <パターン>] \
  [--exclude-images <パターン>] \
  [--include-versions <パターン>] \
  [--exclude-versions <パターン>] \
  [--match-mode prefix|contains] \
  [--dry-run]
```

**オプション**:
- `--source-*`: ソースギャラリーの場所
- `--target-*`: ターゲットギャラリーの場所
- `--tenant-id`: Azure ADテナントID
- `--include-images`: カンマ区切りで含める対象（例: "ubuntu,windows"）
- `--exclude-images`: カンマ区切りで除外対象
- `--include-versions`: バージョン指定で含める対象
- `--exclude-versions`: バージョン指定で除外対象
- `--match-mode`: パターンマッチモード（`prefix` または `contains`、デフォルト: `prefix`）
- `--dry-run`: 変更なしで予定を表示

### `list galleries` - ギャラリー一覧表示

```bash
acg-copy list galleries \
  --subscription <サブスクリプションID> \
  --resource-group <リソースグループ名> \
  --tenant-id <テナントID>
```

### `list images` - イメージ定義一覧表示

```bash
acg-copy list images \
  --subscription <サブスクリプションID> \
  --resource-group <リソースグループ名> \
  --gallery <ギャラリー名> \
  --tenant-id <テナントID>
```

### `list versions` - バージョン一覧表示

```bash
acg-copy list versions \
  --subscription <サブスクリプションID> \
  --resource-group <リソースグループ名> \
  --gallery <ギャラリー名> \
  --image <イメージ名> \
  --tenant-id <テナントID>
```

### `validate` - 設定と接続検証

```bash
acg-copy validate [--config <パス>]
```

---

## 使用例

### すべてのイメージをコピー

```bash
acg-copy copy --config appsettings.json
```

### ドライランで確認

```bash
acg-copy copy --config appsettings.json --dry-run
```

### Ubuntuイメージのみコピー（前方一致）

```bash
acg-copy copy \
  --config appsettings.json \
  --include-images "ubuntu" \
  --match-mode prefix
```

### テストバージョンを除外してコピー

```bash
acg-copy copy \
  --config appsettings.json \
  --exclude-versions "0.0,test"
```

### サブスクリプション内のギャラリーを一覧表示

```bash
acg-copy list galleries \
  --subscription "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" \
  --resource-group "my-rg" \
  --tenant-id "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy"
```

---

## トラブルシューティング

### よくある問題

1. **WebView2 Runtimeが見つからない**
   - ダウンロード: https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/

2. **認証失敗**
   - Azure ADアプリケーション登録が正しく設定されているか確認
   - パブリッククライアントフローが有効か確認
   - 必要な権限が付与されているか確認

3. **クロステナントエラー**
   - このツールは同一テナント内のクロスサブスクリプションのみ対応
   - 両サブスクリプションが同じAzure ADテナントに属しているか確認

4. **アクセス権限エラー（403）**
   - ターゲットサブスクリプションで`Contributor`ロールがあるか確認
   - リソースグループとギャラリーの権限を確認

詳細は[クイックスタートガイド](./specs/001-acg-gallery-copy/quickstart.md#トラブルシューティング)を参照してください。

---

## ドキュメント

- [クイックスタートガイド](./specs/001-acg-gallery-copy/quickstart.md) - 詳細なセットアップと使用方法
- [仕様書](./specs/001-acg-gallery-copy/spec.md) - 機能要件と受け入れ条件
- [設計・アーキテクチャ](./specs/001-acg-gallery-copy/plan.md) - 技術的な設計詳細
- [データモデル](./specs/001-acg-gallery-copy/data-model.md) - エンティティと関連性

---

## 終了コード

- `0`: 成功
- `1`: ドライラン完了（変更なし）
- `2`: 設定検証エラー
- `3`: 復旧可能なエラーで操作失敗
- `4`: 予期しない、または復旧不可能なエラー

---

## ソースからのビルド

### 必要環境

- Visual Studio 2022 以降
- .NET 10 SDK
- MSBuild

### ビルド

```powershell
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -products * -requires Microsoft.Component.MSBuild `
    -find "MSBuild\**\Bin\MSBuild.exe"

& $msbuild "src/AzureComputeGalleryCopy/AzureComputeGalleryCopy.csproj" /m /p:Configuration=Release
```

出力: `src/AzureComputeGalleryCopy/bin/Release/net10.0/AzureComputeGalleryCopy.exe`

---

## テスト実行

```bash
# すべてのテストを実行
dotnet test "tests/AzureComputeGalleryCopy.Tests/AzureComputeGalleryCopy.Tests.csproj"

# 特定のテストを実行
dotnet test "tests/AzureComputeGalleryCopy.Tests/AzureComputeGalleryCopy.Tests.csproj" -k "テスト名"
```

---

## サポート・貢献

問題報告、質問、機能提案はGitHubでissueまたはPull Requestを作成してください。

---

## ライセンス

MIT License - LICENSE ファイルを参照

---

## バージョン情報

**現在のバージョン**: 1.0.0  
**リリース日**: 2025年11月  
**ブランチ**: `001-acg-gallery-copy`
