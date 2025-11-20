# Azure Compute Gallery クロスサブスクリプションコピーツール - クイックスタートガイド

**バージョン**: 1.0.0  
**最終更新**: 2025-11-17

---

## 目次

1. [概要](#概要)
2. [前提条件](#前提条件)
3. [インストール](#インストール)
4. [初期設定](#初期設定)
5. [基本的な使い方](#基本的な使い方)
6. [高度な使い方](#高度な使い方)
7. [トラブルシューティング](#トラブルシューティング)
8. [よくある質問](#よくある質問)
9. [ログの読み方](#ログの読み方)

---

## 概要

このツールは、同一Azure AD テナント内の異なるサブスクリプション間で、Azure Compute Gallery（ACG）のイメージ定義およびイメージバージョンをコピーするためのコマンドラインツールです。

### 主な機能

- ✅ イメージ定義とバージョンの一括コピー
- ✅ 冪等性（同じ操作を何度実行しても安全）
- ✅ フィルタ機能（特定のイメージ・バージョンのみコピー）
- ✅ ドライランモード（実際の変更なしで計画を確認）
- ✅ 詳細なログとエラーレポート
- ✅ 同一テナント内のクロスサブスクリプション対応

### 制約事項

- ❌ クロステナント操作は非対応
- ❌ CMK（Customer-Managed Keys）暗号化イメージは自動スキップ
- ❌ 変更不可能な属性（OS種別、世代、アーキテクチャ）が異なる場合はエラー

---

## 前提条件

### 1. 環境

- **オペレーティングシステム**: **Windows 10/11 または Windows Server 2019/2022**（Windows専用）
- **.NET Runtime**: .NET 10 以降
- **WebView2 Runtime**: Microsoft Edge WebView2 Runtime（[ダウンロード](https://developer.microsoft.com/microsoft-edge/webview2/)）
  - Windows 11には標準搭載
  - Windows 10でMicrosoft Edgeがインストールされている場合は利用可能
  - 未インストールの場合は上記URLからダウンロード
- **ネットワーク**: Azure管理APIへのHTTPSアクセス（ポート443）

> **注意**: このツールはWebView2埋め込み認証を使用するため、**Windows環境専用**です。Webブラウザのキャッシュやセッションとは独立して動作します。

### 2. Azureリソース

#### ソース（コピー元）
- Azure Compute Galleryが存在すること
- ギャラリー内にイメージ定義とバージョンが存在すること

#### ターゲット（コピー先）
- リソースグループが存在すること
- （オプション）Azure Compute Galleryが存在すること（存在しない場合は自動作成可能）

### 3. Azure権限（RBAC）

#### 最小限必要な権限

**ソースサブスクリプション**:
- `Reader`（読み取り）権限
- または `Compute Gallery Sharing Admin`（特定のギャラリーのみ）

**ターゲットサブスクリプション**:
- `Contributor`（共同作成者）権限
- または以下のカスタムロール:
  - `Microsoft.Compute/galleries/read`
  - `Microsoft.Compute/galleries/write`
  - `Microsoft.Compute/galleries/images/read`
  - `Microsoft.Compute/galleries/images/write`
  - `Microsoft.Compute/galleries/images/versions/read`
  - `Microsoft.Compute/galleries/images/versions/write`

#### 権限の確認方法

```bash
# Azure CLIで権限を確認
az role assignment list --assignee <your-user-or-service-principal-id> --subscription <subscription-id>
```

### 4. Azure AD アプリケーション登録

このツールはWebView2埋め込み`InteractiveBrowserCredential`認証を使用するため、Azure ADアプリケーション登録が必要です。

**WebView2認証の特徴**:
- ✅ Webブラウザを起動せず、ツール内の埋め込みWebViewで認証
- ✅ Webブラウザの認証キャッシュに依存しない（ツール専用キャッシュ）
- ✅ Windowsユーザーにとって分かりやすいインタラクティブなログイン体験
- ❌ Windows専用（WebView2 Runtime必須）

#### アプリケーション登録手順

1. **Azure Portalにアクセス**
   - https://portal.azure.com にアクセス
   - 「Azure Active Directory」→「アプリの登録」→「新規登録」

2. **アプリケーション情報の入力**
   - 名前: `ACG Copy Tool`（任意）
   - サポートされているアカウントの種類: `この組織ディレクトリのみのアカウント`
   - リダイレクトURI:
     - プラットフォーム: `パブリック クライアント/ネイティブ (モバイルとデスクトップ)`
     - URI: `http://localhost`

3. **作成後、以下の情報を控える**
   - **アプリケーション（クライアント）ID**: `abcdef01-abcd-abcd-abcd-abcdef012345`（例）
   - **ディレクトリ（テナント）ID**: `12345678-1234-1234-1234-123456789012`（例）

4. **APIのアクセス許可を追加**
   - 「APIのアクセス許可」→「アクセス許可の追加」
   - 「Azure Service Management」を選択
   - 「委任されたアクセス許可」→「user_impersonation」を選択
   - 「アクセス許可の追加」をクリック
   - **重要**: 「管理者の同意を与える」をクリック（テナント管理者権限が必要）

5. **認証設定**
   - 「認証」→「詳細設定」
   - 「パブリック クライアント フローを許可する」を **はい** に設定
   - 保存

#### WebView2 Runtimeの確認

ツール起動時に自動的にWebView2 Runtimeの有無を確認します。未インストールの場合はエラーメッセージが表示されます。

**手動確認方法**:

```powershell
# PowerShellでバージョン確認
Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -Name pv | Select-Object -ExpandProperty pv
```

表示例: `130.0.2849.68`（バージョン番号が表示されればインストール済み）

**未インストールの場合**:
- [Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) からダウンロード
- "Evergreen Standalone Installer" を推奨

---

## インストール

### オプション1: リリースバイナリ（推奨）

1. [Releases](https://github.com/your-org/acg-copy-tool/releases)ページから最新バージョンをダウンロード
   - **Windows x64**: `acg-copy-win-x64.zip`（Windows 10/11, Server 2019/2022専用）

2. ファイルを展開

```powershell
# Windows
Expand-Archive acg-copy-win-x64.zip -DestinationPath C:\Tools\acg-copy
```

3. 環境変数PATHに追加（オプション）

```powershell
# Windows (PowerShell)
$env:PATH += ";C:\Tools\acg-copy"
```

### オプション2: ソースからビルド

```powershell
# リポジトリをクローン
git clone https://github.com/your-org/acg-copy-tool.git
cd acg-copy-tool

# MSBuildでビルド
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -products * -requires Microsoft.Component.MSBuild `
    -find "MSBuild\**\Bin\MSBuild.exe"

& $msbuild "src\AzureComputeGalleryCopy\AzureComputeGalleryCopy.csproj" /m /p:Configuration=Release

# バイナリは src\AzureComputeGalleryCopy\bin\Release\net10.0\ に生成
```

---

## 初期設定

### 1. 設定ファイルの作成

ツールのディレクトリに `appsettings.json` を作成します。

```json
{
  "source": {
    "subscriptionId": "あなたのソースサブスクリプションID",
    "resourceGroupName": "ソースのリソースグループ名",
    "galleryName": "ソースのギャラリー名"
  },
  "target": {
    "subscriptionId": "あなたのターゲットサブスクリプションID",
    "resourceGroupName": "ターゲットのリソースグループ名",
    "galleryName": "ターゲットのギャラリー名"
  },
  "authentication": {
    "tenantId": "あなたのテナントID",
    "clientId": "アプリ登録のクライアントID"
  },
  "filter": {
    "imageDefinitionIncludes": [],
    "imageDefinitionExcludes": [],
    "versionIncludes": [],
    "versionExcludes": [],
    "matchMode": "prefix"
  },
  "dryRun": false,
  "logLevel": "Information"
}
```

### 2. 実際の値に置き換え

```json
{
  "source": {
    "subscriptionId": "11111111-1111-1111-1111-111111111111",
    "resourceGroupName": "production-rg",
    "galleryName": "prod-gallery"
  },
  "target": {
    "subscriptionId": "22222222-2222-2222-2222-222222222222",
    "resourceGroupName": "staging-rg",
    "galleryName": "staging-gallery"
  },
  "authentication": {
    "tenantId": "12345678-1234-1234-1234-123456789012",
    "clientId": "abcdef01-abcd-abcd-abcd-abcdef012345"
  },
  "filter": {
    "imageDefinitionIncludes": [],
    "imageDefinitionExcludes": [],
    "versionIncludes": [],
    "versionExcludes": [],
    "matchMode": "prefix"
  },
  "dryRun": false,
  "logLevel": "Information"
}
```

### 3. 設定の検証

```bash
acg-copy validate --config appsettings.json
```

**期待される出力**:
```
✓ Configuration file loaded successfully
✓ Source context validated
✓ Target context validated
✓ Same tenant constraint satisfied
✓ Authentication configuration valid

All validations passed.
```

**エラーが出た場合**: [トラブルシューティング](#トラブルシューティング)を参照

---

## 基本的な使い方

### 1. ドライランで計画を確認

実際の変更を行う前に、ドライランモードで何が行われるか確認します。

```bash
acg-copy copy --config appsettings.json --dry-run
```

または設定ファイルなしでCLIオプションのみ:

```bash
acg-copy copy \
  --source-subscription "11111111-1111-1111-1111-111111111111" \
  --source-resource-group "production-rg" \
  --source-gallery "prod-gallery" \
  --target-subscription "22222222-2222-2222-2222-222222222222" \
  --target-resource-group "staging-rg" \
  --target-gallery "staging-gallery" \
  --tenant-id "12345678-1234-1234-1234-123456789012" \
  --client-id "abcdef01-abcd-abcd-abcd-abcdef012345" \
  --dry-run
```

**期待される出力**:
```
[INFO] DRY RUN MODE: No resources will be created

[INFO] Step 1/4: Authenticating...
To sign in, use a web browser to open the page https://microsoft.com/devicelogin and enter the code ABCD1234 to authenticate.
[INFO] ✓ Authentication complete

[INFO] Step 2/4: Listing source images...
[INFO] ✓ Found 3 image definitions, 12 versions

[INFO] Step 3/4: Analyzing copy plan...
[INFO]   Image definition 'ubuntu-2204':
[INFO]     → Will create image definition
[INFO]     Version '1.0.0':
[INFO]       → Will create version
[INFO]     Version '1.0.1':
[WARN]       → Will skip (already exists)

========================================
Copy Plan Summary
========================================
Planned Operations:
  Image Definitions to Create: 2
  Image Versions to Create: 10
  Image Versions to Skip: 2

To execute this plan, run the same command without --dry-run.
```

### 2. 認証

ツール実行時に、Device Code認証が開始されます。

```
To sign in, use a web browser to open the page https://microsoft.com/devicelogin and enter the code ABCD1234 to authenticate.
```

1. ブラウザで https://microsoft.com/devicelogin を開く
2. 表示されたコード（例: `ABCD1234`）を入力
3. Azure ADアカウントでサインイン
4. 「このアプリケーションに権限を与えてよろしいですか？」→「はい」をクリック

### 3. 実際のコピー実行

ドライランの結果を確認し、問題なければ実際にコピーを実行します。

```bash
acg-copy copy --config appsettings.json
```

**期待される出力**:
```
[INFO] Starting copy operation
[INFO] Source: Subscription '11111111...', Gallery 'prod-gallery'
[INFO] Target: Subscription '22222222...', Gallery 'staging-gallery'

[INFO] Step 1/4: Authenticating...
[INFO] ✓ Authentication complete

[INFO] Step 2/4: Listing source images...
[INFO] ✓ Found 3 image definitions, 12 versions

[INFO] Step 3/4: Copying images...
[INFO]   Copying image definition 'ubuntu-2204'...
[INFO]     ✓ Image definition created
[INFO]     Copying version '1.0.0'...
[INFO]       ✓ Version created (ID: /subscriptions/22222222.../versions/1.0.0)
[INFO]     Copying version '1.0.1'...
[WARN]       Version already exists, skipping

[INFO] Step 4/4: Complete

========================================
Copy Summary
========================================
Duration: 00:15:32
Results:
  Image Definitions Created: 2
  Image Versions Created: 10
  Image Versions Skipped: 2
  Failed Operations: 0
```

---

## 高度な使い方

### 1. 特定のイメージのみコピー

#### パターン: 前方一致（デフォルト）

```bash
acg-copy copy \
  --config appsettings.json \
  --include-images "ubuntu,windows-server" \
  --match-mode prefix
```

これにより以下がコピーされます:
- `ubuntu`で始まるイメージ: `ubuntu-2004`, `ubuntu-2204`
- `windows-server`で始まるイメージ: `windows-server-2019`, `windows-server-2022`

#### パターン: 部分一致

```bash
acg-copy copy \
  --config appsettings.json \
  --include-images "ubuntu" \
  --match-mode contains
```

これにより以下がコピーされます:
- `ubuntu`を含むイメージ: `my-ubuntu-image`, `ubuntu-server`, `custom-ubuntu`

### 2. 特定のイメージを除外

```bash
acg-copy copy \
  --config appsettings.json \
  --exclude-images "test,deprecated,old"
```

### 3. 特定のバージョンのみコピー

```bash
# 1.0系のバージョンのみコピー
acg-copy copy \
  --config appsettings.json \
  --include-versions "1.0" \
  --match-mode prefix

# 0.0系のバージョンを除外
acg-copy copy \
  --config appsettings.json \
  --exclude-versions "0.0"
```

### 4. フィルタの組み合わせ

```bash
acg-copy copy \
  --config appsettings.json \
  --include-images "ubuntu" \
  --exclude-images "test" \
  --include-versions "1.0,2.0" \
  --exclude-versions "0.0" \
  --match-mode prefix
```

処理順序:
1. `ubuntu`で始まるイメージを選択
2. `test`で始まるイメージを除外
3. 残ったイメージの中で、`1.0`または`2.0`で始まるバージョンを選択
4. `0.0`で始まるバージョンを除外

### 5. 環境変数を使用

設定ファイルの代わりに環境変数で設定可能:

```powershell
# PowerShell
$env:ACG_COPY_TENANT_ID = "12345678-1234-1234-1234-123456789012"
$env:ACG_COPY_CLIENT_ID = "abcdef01-abcd-abcd-abcd-abcdef012345"
$env:ACG_COPY_SOURCE_SUBSCRIPTION = "11111111-1111-1111-1111-111111111111"
$env:ACG_COPY_SOURCE_RESOURCE_GROUP = "production-rg"
$env:ACG_COPY_SOURCE_GALLERY = "prod-gallery"
$env:ACG_COPY_TARGET_SUBSCRIPTION = "22222222-2222-2222-2222-222222222222"
$env:ACG_COPY_TARGET_RESOURCE_GROUP = "staging-rg"
$env:ACG_COPY_TARGET_GALLERY = "staging-gallery"

acg-copy copy
```

```bash
# Bash
export ACG_COPY_TENANT_ID="12345678-1234-1234-1234-123456789012"
export ACG_COPY_CLIENT_ID="abcdef01-abcd-abcd-abcd-abcdef012345"
export ACG_COPY_SOURCE_SUBSCRIPTION="11111111-1111-1111-1111-111111111111"
export ACG_COPY_SOURCE_RESOURCE_GROUP="production-rg"
export ACG_COPY_SOURCE_GALLERY="prod-gallery"
export ACG_COPY_TARGET_SUBSCRIPTION="22222222-2222-2222-2222-222222222222"
export ACG_COPY_TARGET_RESOURCE_GROUP="staging-rg"
export ACG_COPY_TARGET_GALLERY="staging-gallery"

acg-copy copy
```

### 6. ログレベルの調整

```bash
# デバッグ情報を表示
acg-copy copy --config appsettings.json --log-level Debug

# 警告とエラーのみ表示
acg-copy copy --config appsettings.json --log-level Warning
```

### 7. JSON出力（スクリプト連携用）

```bash
acg-copy copy --config appsettings.json --output json > result.json
```

---

## トラブルシューティング

### エラー: WebView2 Runtime未インストール

#### エラーメッセージ

```
[ERROR] WebView2 Runtime is not installed.
Please download and install from: https://developer.microsoft.com/microsoft-edge/webview2/
```

#### 原因

WebView2 Runtimeがインストールされていない。

#### 解決方法

1. [Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) にアクセス
2. "Evergreen Standalone Installer" をダウンロード
3. インストール後、ツールを再実行

#### 確認方法

```powershell
# PowerShellでバージョン確認
Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -Name pv | Select-Object -ExpandProperty pv
```

バージョン番号が表示されればインストール済み（例: `130.0.2849.68`）

---

### エラー: 設定検証失敗

#### エラーメッセージ

```
Error: Source and target must be in the same tenant.
  Source TenantId: 12345678-1234-1234-1234-123456789012
  Target TenantId: 87654321-4321-4321-4321-210987654321
```

#### 原因

ソースとターゲットのサブスクリプションが異なるテナントに属している。

#### 解決方法

1. Azure Portalで両サブスクリプションのテナントIDを確認
2. 同一テナント内のサブスクリプションを指定
3. クロステナントコピーが必要な場合は、このツールではサポート外

#### 確認コマンド

```bash
# Azure CLIでテナントIDを確認
az account show --subscription "11111111-1111-1111-1111-111111111111" --query tenantId
az account show --subscription "22222222-2222-2222-2222-222222222222" --query tenantId
```

---

### エラー: 認証失敗

#### エラーメッセージ例1: アプリケーションIDエラー

```
[ERROR] Authentication failed
  Error Code: AADSTS700016
  Message: Application with identifier 'abcdef01-abcd-abcd-abcd-abcdef012345' was not found in the directory '12345678-1234-1234-1234-123456789012'.
```

#### 原因

1. クライアントIDが間違っている
2. アプリケーション登録が削除されている
3. テナントIDが間違っている

#### 解決方法

1. Azure Portalで「Azure Active Directory」→「アプリの登録」を開く
2. アプリケーション名で検索し、存在を確認
3. 「概要」ページで「アプリケーション（クライアント）ID」と「ディレクトリ（テナント）ID」を確認
4. `appsettings.json`の値を正しい値に更新

#### エラーメッセージ例2: パブリッククライアントフロー無効

```
[ERROR] Authentication failed
  Error Code: AADSTS7000218
  Message: The request body must contain the following parameter: 'client_assertion' or 'client_secret'.
```

#### 原因

アプリケーション登録で「パブリック クライアント フローを許可する」が無効になっている。

#### 解決方法

1. Azure Portalで「Azure Active Directory」→「アプリの登録」→該当アプリを開く
2. 「認証」→「詳細設定」
3. 「パブリック クライアント フローを許可する」を **はい** に変更
4. 保存

#### エラーメッセージ例3: WebView2ウィンドウが表示されない

**症状**: ログインウィンドウが表示されず、タイムアウトエラーが発生

#### 原因

1. WebView2 Runtimeが破損している
2. ファイアウォールがWebView2をブロック
3. ディスプレイドライバの問題

#### 解決方法

1. **WebView2 Runtimeを再インストール**:
   ```powershell
   # アンインストール（存在する場合）
   Get-Package -Name "Microsoft Edge WebView2 Runtime" | Uninstall-Package
   
   # 再インストール（ダウンロード後）
   # https://developer.microsoft.com/microsoft-edge/webview2/
   ```

2. **ファイアウォール設定確認**:
   - `login.microsoftonline.com`へのHTTPSアクセスを許可
   - ツール実行ファイルをファイアウォール例外に追加

3. **ディスプレイ設定**:
   - リモートデスクトップ使用時は、ローカルでの実行を試行
   - 高DPI設定を無効化してテスト

---

### エラー: 権限不足

#### エラーメッセージ

```
[ERROR] Failed to copy image version 'ubuntu-2204/1.0.0'
  HTTP Status: 403
  Error Code: AuthorizationFailed
  Message: The client 'user@example.com' does not have authorization to perform action 'Microsoft.Compute/galleries/images/versions/write' over scope '/subscriptions/22222222.../galleries/staging-gallery/images/ubuntu-2204/versions/1.0.0'
```

#### 原因

ターゲットサブスクリプションまたはリソースグループに対する書き込み権限がない。

#### 解決方法

1. Azure Portalでターゲットサブスクリプションを開く
2. 「アクセス制御(IAM)」を開く
3. 「ロールの割り当て」で自分のアカウントに`Contributor`ロールがあるか確認
4. ない場合は、サブスクリプション管理者に権限付与を依頼

#### 必要な権限の詳細

**最小限**（カスタムロール）:
```json
{
  "actions": [
    "Microsoft.Compute/galleries/read",
    "Microsoft.Compute/galleries/write",
    "Microsoft.Compute/galleries/images/read",
    "Microsoft.Compute/galleries/images/write",
    "Microsoft.Compute/galleries/images/versions/read",
    "Microsoft.Compute/galleries/images/versions/write"
  ]
}
```

**推奨**:
- `Contributor`ロール（リソースグループまたはサブスクリプションレベル）

---

### エラー: イメージ定義の属性不一致

#### エラーメッセージ

```
[ERROR] Image definition 'ubuntu-2204' already exists with incompatible attributes
  Attribute: OSType
  Source: Linux
  Target: Windows
```

#### 原因

ターゲットギャラリーに同名のイメージ定義が既に存在し、OS種別などの変更不可能な属性が異なる。

#### 解決方法

**オプション1**: ターゲットの既存イメージ定義を削除

```bash
# Azure CLIで削除
az sig image-definition delete \
  --gallery-name "staging-gallery" \
  --gallery-image-definition "ubuntu-2204" \
  --resource-group "staging-rg"
```

**オプション2**: ソース側のイメージ定義名を変更（新しい名前でコピー）

**オプション3**: フィルタで当該イメージを除外

```bash
acg-copy copy --config appsettings.json --exclude-images "ubuntu-2204"
```

---

### エラー: リージョン利用不可

#### エラーメッセージ

```
[WARN] Skipping version 'ubuntu-2204/1.0.0': Target region 'westus3' is not available in target subscription
```

#### 原因

ソースイメージのレプリケーション対象リージョンが、ターゲットサブスクリプションで利用できない。

#### 解決方法

これは警告であり、エラーではありません。当該バージョンはスキップされます。

**対応が必要な場合**:
1. ターゲットサブスクリプションで当該リージョンを有効化（サブスクリプション管理者に依頼）
2. ソースイメージのレプリケーション設定を変更し、利用可能なリージョンのみに設定

---

### エラー: レート制限

#### エラーメッセージ

```
[WARN] Rate limited by Azure API, retry after 60 seconds
[ERROR] Azure API error 429: TooManyRequests
```

#### 原因

Azure APIの呼び出し制限に達した。

#### 解決方法

1. **待機**: エラーメッセージに表示された時間（例: 60秒）待ってから再実行
2. **フィルタを使用**: 一度にコピーする対象を減らす
3. **分割実行**: イメージ定義ごとに分けて実行

```bash
# 例: ubuntu系のみ先に実行
acg-copy copy --config appsettings.json --include-images "ubuntu"

# 次にwindows系を実行
acg-copy copy --config appsettings.json --include-images "windows"
```

---

### エラー: ネットワーク接続

#### エラーメッセージ

```
[ERROR] Failed to connect to Azure management API
  Message: No such host is known (management.azure.com)
```

#### 原因

1. インターネット接続がない
2. プロキシ設定が必要
3. ファイアウォールがHTTPS（ポート443）をブロック

#### 解決方法

1. **インターネット接続を確認**:
   ```bash
   ping management.azure.com
   ```

2. **プロキシ設定**（必要な場合）:
   ```powershell
   # PowerShell
   $env:HTTPS_PROXY = "http://proxy.example.com:8080"
   ```

3. **ファイアウォール設定**:
   - `*.azure.com`へのHTTPS（ポート443）アクセスを許可
   - `login.microsoftonline.com`へのアクセスを許可

---

## よくある質問

### Q1: ツール実行中に中断した場合、データは破損しますか?

**A**: いいえ、破損しません。このツールは冪等性を保証しているため、中断した時点までに作成されたリソースはそのまま残り、再実行時はスキップされます。

**例**:
- 10個のバージョンをコピー中、5個目で中断
- 再実行時は1-4個目はスキップされ、5個目から続行

---

### Q2: 同じコマンドを複数回実行しても安全ですか?

**A**: はい、安全です。既に存在するリソースは自動的にスキップされます。

```
[WARN] Image definition 'ubuntu-2204' already exists, skipping
[WARN] Image version 'ubuntu-2204/1.0.0' already exists, skipping
```

---

### Q3: 大量のイメージをコピーする場合、どのくらい時間がかかりますか?

**A**: 以下の要因に依存します:

- **イメージ定義の作成**: 約10-30秒/定義
- **イメージバージョンの作成**: 約5-15分/バージョン（イメージサイズとレプリケーション地域数に依存）

**例**:
- 10定義、100バージョン、平均10分/バージョン: 約16-17時間

**推奨**:
- 大量コピーは夜間や週末に実行
- 分割実行を検討

---

### Q4: クロステナント間でコピーしたい場合はどうすればよいですか?

**A**: このツールはクロステナントをサポートしていません。以下の代替手段を検討してください:

1. **Azure Portal/CLI手動コピー**: 各イメージを手動でエクスポート/インポート
2. **VHDエクスポート**: VHDファイルとしてエクスポートし、別テナントで再作成
3. **Azure Image Builder**: イメージビルドパイプラインを構築

---

### Q5: ツールの実行ログをファイルに保存したい

**A**: 標準出力をリダイレクトしてください:

```bash
# PowerShell
acg-copy copy --config appsettings.json 2>&1 | Tee-Object -FilePath "copy-log.txt"

# Bash
acg-copy copy --config appsettings.json 2>&1 | tee copy-log.txt
```

---

## ログの読み方

### ログレベル

- `[TRACE]`: 詳細な内部処理（デフォルトでは表示されない）
- `[DEBUG]`: デバッグ情報（`--log-level Debug`で表示）
- `[INFO]`: 通常の進捗情報
- `[WARN]`: 警告（処理は継続）
- `[ERROR]`: エラー（処理失敗）
- `[CRITICAL]`: 致命的エラー（ツール停止）

### 重要なログパターン

#### 成功

```
[INFO] ✓ Image definition created
[INFO] ✓ Version created (ID: /subscriptions/22222222.../versions/1.0.0)
```

#### スキップ（冪等性）

```
[WARN] Image definition 'ubuntu-2204' already exists, skipping
[WARN] Image version 'ubuntu-2204/1.0.0' already exists, skipping
```

#### スキップ（制約）

```
[WARN] Skipping version 'ubuntu-2204/1.0.0': Target region 'westus3' is not available
[WARN] Skipping version 'ubuntu-2204/1.0.1': CMK encryption not supported for cross-subscription copy
```

#### エラー

```
[ERROR] Failed to copy image version 'ubuntu-2204/1.0.0'
  Operation ID: 550e8400-e29b-41d4-a716-446655440000
  Resource ID: /subscriptions/22222222.../versions/1.0.0
  HTTP Status: 403
  Error Code: AuthorizationFailed
  Message: The client does not have authorization...
```

### エラー発生時のデバッグ手順

1. **エラーメッセージを確認**:
   - `HTTP Status`: HTTPステータスコード（403=権限不足、404=リソース不在、429=レート制限等）
   - `Error Code`: Azureエラーコード
   - `Message`: 詳細メッセージ

2. **Operation IDで関連ログを検索**:
   ```bash
   # ログファイル内でOperation IDを検索
   grep "550e8400-e29b-41d4-a716-446655440000" copy-log.txt
   ```

3. **Resource IDでAzure Portalを確認**:
   - Resource IDをコピーしてAzure Portalで検索
   - リソースの状態、権限、設定を確認

4. **前後のログを確認**:
   - エラー発生前の操作
   - エラー発生時のコンテキスト（どのイメージ、どのバージョン）

---

## サポート

### 問題報告

GitHubでIssueを作成してください: https://github.com/your-org/acg-copy-tool/issues

**必要な情報**:
- ツールバージョン（`acg-copy --version`）
- OS（Windows/Linux/macOS）
- エラーメッセージ全文
- ログファイル（機密情報を削除）
- 再現手順

### ライセンス

MIT License

---

**最終更新**: 2025-11-17  
**バージョン**: 1.0.0
