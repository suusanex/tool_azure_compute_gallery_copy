# CLI Interface Contract

**日付**: 2025-11-17  
**目的**: コマンドラインインターフェースの仕様定義

---

## Command Structure（コマンド構造）

### Root Command

```
acg-copy [options] <command>
```

**説明**: Azure Compute Galleryイメージのクロスサブスクリプションコピーツール

**Global Options**:
- `--config <path>` / `-c <path>`: 設定ファイルのパス（デフォルト: `appsettings.json`）
- `--log-level <level>` / `-l <level>`: ログレベル（Trace, Debug, Information, Warning, Error, Critical）（デフォルト: Information）
- `--help` / `-h`: ヘルプを表示
- `--version` / `-v`: バージョン情報を表示

---

## Commands（コマンド）

### 1. copy - イメージコピー

```
acg-copy copy [options]
```

**説明**: ソースギャラリーからターゲットギャラリーへイメージをコピー

**Required Options**:
- `--source-subscription <id>` / `-ss <id>`: ソースサブスクリプションID
- `--source-resource-group <name>` / `-srg <name>`: ソースリソースグループ名
- `--source-gallery <name>` / `-sg <name>`: ソースギャラリー名
- `--target-subscription <id>` / `-ts <id>`: ターゲットサブスクリプションID
- `--target-resource-group <name>` / `-trg <name>`: ターゲットリソースグループ名
- `--target-gallery <name>` / `-tg <name>`: ターゲットギャラリー名

**Optional Options**:
- `--tenant-id <id>` / `-t <id>`: テナントID（設定ファイルまたは環境変数から読み取り可能）
- `--client-id <id>`: アプリ登録のクライアントID（設定ファイルまたは環境変数から読み取り可能）
- `--include-images <patterns>` / `-ii <patterns>`: コピー対象イメージ定義名のパターン（カンマ区切り）
- `--exclude-images <patterns>` / `-ei <patterns>`: 除外対象イメージ定義名のパターン（カンマ区切り）
- `--include-versions <patterns>` / `-iv <patterns>`: コピー対象バージョン名のパターン（カンマ区切り）
- `--exclude-versions <patterns>` / `-ev <patterns>`: 除外対象バージョン名のパターン（カンマ区切り）
- `--match-mode <mode>` / `-mm <mode>`: パターンマッチ方式（prefix, contains）（デフォルト: prefix）
- `--dry-run` / `-d`: ドライランモード（実際の作成なし、計画のみ表示）

**例**:

```bash
# 全イメージコピー
acg-copy copy \
  --source-subscription "11111111-1111-1111-1111-111111111111" \
  --source-resource-group "source-rg" \
  --source-gallery "source-gallery" \
  --target-subscription "22222222-2222-2222-2222-222222222222" \
  --target-resource-group "target-rg" \
  --target-gallery "target-gallery"

# 特定イメージのみコピー
acg-copy copy \
  -ss "11111111-1111-1111-1111-111111111111" \
  -srg "source-rg" \
  -sg "source-gallery" \
  -ts "22222222-2222-2222-2222-222222222222" \
  -trg "target-rg" \
  -tg "target-gallery" \
  --include-images "ubuntu,windows" \
  --match-mode prefix

# ドライラン
acg-copy copy \
  -ss "11111111-1111-1111-1111-111111111111" \
  -srg "source-rg" \
  -sg "source-gallery" \
  -ts "22222222-2222-2222-2222-222222222222" \
  -trg "target-rg" \
  -tg "target-gallery" \
  --dry-run
```

**終了コード**:
- `0`: 成功（すべての操作が成功またはスキップ）
- `1`: 部分的失敗（一部の操作が失敗）
- `2`: 全失敗（すべての操作が失敗）
- `3`: 設定エラー
- `4`: 認証エラー

---

### 2. list - リソース一覧

#### 2.1 list galleries - ギャラリー一覧

```
acg-copy list galleries [options]
```

**説明**: 指定サブスクリプションのギャラリー一覧を表示

**Required Options**:
- `--subscription <id>` / `-s <id>`: サブスクリプションID
- `--resource-group <name>` / `-rg <name>`: リソースグループ名

**Optional Options**:
- `--tenant-id <id>` / `-t <id>`: テナントID
- `--output <format>` / `-o <format>`: 出力形式（text, json）（デフォルト: text）

**例**:

```bash
# テキスト形式で表示
acg-copy list galleries \
  --subscription "11111111-1111-1111-1111-111111111111" \
  --resource-group "my-rg"

# JSON形式で表示
acg-copy list galleries \
  -s "11111111-1111-1111-1111-111111111111" \
  -rg "my-rg" \
  -o json
```

**出力例（text）**:

```
Galleries in subscription '11111111-1111-1111-1111-111111111111', resource group 'my-rg':

  Name: gallery1
  Location: eastus
  Description: My first gallery

  Name: gallery2
  Location: westus
  Description: My second gallery
```

**出力例（json）**:

```json
[
  {
    "name": "gallery1",
    "location": "eastus",
    "description": "My first gallery"
  },
  {
    "name": "gallery2",
    "location": "westus",
    "description": "My second gallery"
  }
]
```

---

#### 2.2 list images - イメージ定義一覧

```
acg-copy list images [options]
```

**説明**: 指定ギャラリーのイメージ定義一覧を表示

**Required Options**:
- `--subscription <id>` / `-s <id>`: サブスクリプションID
- `--resource-group <name>` / `-rg <name>`: リソースグループ名
- `--gallery <name>` / `-g <name>`: ギャラリー名

**Optional Options**:
- `--tenant-id <id>` / `-t <id>`: テナントID
- `--output <format>` / `-o <format>`: 出力形式（text, json）（デフォルト: text）

**例**:

```bash
# イメージ定義一覧
acg-copy list images \
  --subscription "11111111-1111-1111-1111-111111111111" \
  --resource-group "my-rg" \
  --gallery "my-gallery"
```

**出力例（text）**:

```
Image definitions in gallery 'my-gallery':

  Name: ubuntu-2204
  OS Type: Linux
  OS State: Specialized
  Hyper-V Generation: V2
  Architecture: x64
  Versions: 5

  Name: windows-server-2022
  OS Type: Windows
  OS State: Generalized
  Hyper-V Generation: V2
  Architecture: x64
  Versions: 3
```

---

#### 2.3 list versions - イメージバージョン一覧

```
acg-copy list versions [options]
```

**説明**: 指定イメージ定義のバージョン一覧を表示

**Required Options**:
- `--subscription <id>` / `-s <id>`: サブスクリプションID
- `--resource-group <name>` / `-rg <name>`: リソースグループ名
- `--gallery <name>` / `-g <name>`: ギャラリー名
- `--image <name>` / `-i <name>`: イメージ定義名

**Optional Options**:
- `--tenant-id <id>` / `-t <id>`: テナントID
- `--output <format>` / `-o <format>`: 出力形式（text, json）（デフォルト: text）

**例**:

```bash
# バージョン一覧
acg-copy list versions \
  --subscription "11111111-1111-1111-1111-111111111111" \
  --resource-group "my-rg" \
  --gallery "my-gallery" \
  --image "ubuntu-2204"
```

**出力例（text）**:

```
Versions for image 'ubuntu-2204':

  Version: 1.0.0
  Target Regions: eastus (1 replica), westus (1 replica)
  Exclude From Latest: No
  End of Life: None

  Version: 1.0.1
  Target Regions: eastus (2 replicas), westus (2 replicas), northeurope (1 replica)
  Exclude From Latest: No
  End of Life: 2026-01-01
```

---

### 3. validate - 設定検証

```
acg-copy validate [options]
```

**説明**: 設定ファイルおよび接続情報を検証

**Optional Options**:
- `--config <path>` / `-c <path>`: 設定ファイルのパス（デフォルト: `appsettings.json`）

**例**:

```bash
# 設定検証
acg-copy validate --config appsettings.json
```

**出力例（成功）**:

```
✓ Configuration file loaded successfully
✓ Source context validated
  - Subscription: 11111111-1111-1111-1111-111111111111
  - Resource Group: source-rg
  - Gallery: source-gallery
✓ Target context validated
  - Subscription: 22222222-2222-2222-2222-222222222222
  - Resource Group: target-rg
  - Gallery: target-gallery
✓ Same tenant constraint satisfied
  - Tenant ID: 33333333-3333-3333-3333-333333333333
✓ Authentication configuration valid

All validations passed.
```

**出力例（エラー）**:

```
✗ Configuration validation failed

Error: Source and target must be in the same tenant.
  Source TenantId: 33333333-3333-3333-3333-333333333333
  Target TenantId: 44444444-4444-4444-4444-444444444444

To fix: Ensure both subscriptions belong to the same Azure AD tenant.
```

---

## Output Formats（出力形式）

### Standard Output（標準出力）

#### Progress Output（進捗出力）

コピー操作中の進捗表示:

```
[INFO] Starting copy operation
[INFO] Source: Subscription '11111111...', Gallery 'source-gallery'
[INFO] Target: Subscription '22222222...', Gallery 'target-gallery'

[INFO] Step 1/4: Authenticating...
To sign in, use a web browser to open the page https://microsoft.com/devicelogin and enter the code ABCD1234 to authenticate.
[INFO] ✓ Authentication complete

[INFO] Step 2/4: Listing source images...
[INFO] ✓ Found 3 image definitions, 12 versions

[INFO] Step 3/4: Copying images...
[INFO]   Copying image definition 'ubuntu-2204'...
[INFO]     ✓ Image definition created
[INFO]     Copying version '1.0.0'...
[INFO]       ✓ Version created
[INFO]     Copying version '1.0.1'...
[WARN]       Version already exists, skipping
[INFO]   Copying image definition 'windows-server-2022'...
[WARN]     Image definition already exists with incompatible OS type (Source: Windows, Target: Linux)
[ERROR]     Skipping image definition 'windows-server-2022'

[INFO] Step 4/4: Complete

========================================
Copy Summary
========================================
Duration: 00:15:32
Source: Subscription '11111111...', Gallery 'source-gallery'
Target: Subscription '22222222...', Gallery 'target-gallery'

Results:
  Image Definitions Created: 2
  Image Versions Created: 10
  Image Versions Skipped: 2
  Failed Operations: 1

Skipped Operations:
  - Version 'ubuntu-2204/1.0.1': Already exists
  - Version 'ubuntu-2204/1.0.2': Target region 'invalidregion' not available

Failed Operations:
  - Image 'windows-server-2022': Incompatible OS type (Source: Windows, Target: Linux)
```

#### Dry Run Output（ドライラン出力）

```
[INFO] DRY RUN MODE: No resources will be created

[INFO] Step 1/4: Authenticating...
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
[INFO]   Image definition 'windows-server-2022':
[INFO]     → Image definition already exists
[INFO]     Version '1.0.0':
[INFO]       → Will create version
[INFO]     Version '1.0.1':
[INFO]       → Will create version

[INFO] Step 4/4: Complete

========================================
Copy Plan Summary
========================================
Source: Subscription '11111111...', Gallery 'source-gallery'
Target: Subscription '22222222...', Gallery 'target-gallery'

Planned Operations:
  Image Definitions to Create: 1
  Image Versions to Create: 11
  Image Versions to Skip: 1

To execute this plan, run the same command without --dry-run.
```

---

### JSON Output（JSON出力）

`--output json`オプション使用時のJSON形式出力:

```json
{
  "startTime": "2025-11-17T10:00:00Z",
  "endTime": "2025-11-17T10:15:32Z",
  "sourceContext": {
    "tenantId": "33333333-3333-3333-3333-333333333333",
    "subscriptionId": "11111111-1111-1111-1111-111111111111",
    "resourceGroupName": "source-rg",
    "galleryName": "source-gallery"
  },
  "targetContext": {
    "tenantId": "33333333-3333-3333-3333-333333333333",
    "subscriptionId": "22222222-2222-2222-2222-222222222222",
    "resourceGroupName": "target-rg",
    "galleryName": "target-gallery"
  },
  "operations": [
    {
      "operationId": "550e8400-e29b-41d4-a716-446655440000",
      "type": "CreateImageDefinition",
      "imageDefinitionName": "ubuntu-2204",
      "result": "Success",
      "startTime": "2025-11-17T10:05:00Z",
      "endTime": "2025-11-17T10:05:10Z"
    },
    {
      "operationId": "550e8400-e29b-41d4-a716-446655440001",
      "type": "CreateImageVersion",
      "imageDefinitionName": "ubuntu-2204",
      "versionName": "1.0.0",
      "result": "Success",
      "startTime": "2025-11-17T10:05:11Z",
      "endTime": "2025-11-17T10:10:00Z"
    },
    {
      "operationId": "550e8400-e29b-41d4-a716-446655440002",
      "type": "CreateImageVersion",
      "imageDefinitionName": "ubuntu-2204",
      "versionName": "1.0.1",
      "result": "Skipped",
      "skipReason": "Version already exists in target gallery",
      "startTime": "2025-11-17T10:10:01Z",
      "endTime": "2025-11-17T10:10:02Z"
    }
  ],
  "createdImageDefinitions": 2,
  "createdImageVersions": 10,
  "skippedImageVersions": 2,
  "failedOperations": 1,
  "isDryRun": false
}
```

---

## Error Output（エラー出力）

エラーは標準エラー出力（stderr）に出力:

```
[ERROR] Failed to copy image version 'ubuntu-2204/1.0.0'
  Operation ID: 550e8400-e29b-41d4-a716-446655440000
  Resource ID: /subscriptions/22222222.../galleries/target-gallery/images/ubuntu-2204/versions/1.0.0
  HTTP Status: 403
  Error Code: AuthorizationFailed
  Message: The client '...' does not have authorization to perform action 'Microsoft.Compute/galleries/images/versions/write' over scope '...'

  To fix: Ensure the service principal has 'Contributor' or 'Gallery Image Version Contributor' role on the target resource group.

[ERROR] Azure API exception:
Azure.RequestFailedException: The client '...' does not have authorization to perform action 'Microsoft.Compute/galleries/images/versions/write' over scope '...'
   at Azure.ResourceManager.Compute.GalleryImageVersionCollection.CreateOrUpdateAsync(...)
   at AzureComputeGalleryCopy.Services.GalleryCopyService.CopyImageVersionAsync(...)
```

---

## Environment Variables（環境変数）

CLIオプションの代わりに環境変数を使用可能:

- `ACG_COPY_TENANT_ID`: テナントID
- `ACG_COPY_CLIENT_ID`: アプリ登録のクライアントID
- `ACG_COPY_SOURCE_SUBSCRIPTION`: ソースサブスクリプションID
- `ACG_COPY_SOURCE_RESOURCE_GROUP`: ソースリソースグループ名
- `ACG_COPY_SOURCE_GALLERY`: ソースギャラリー名
- `ACG_COPY_TARGET_SUBSCRIPTION`: ターゲットサブスクリプションID
- `ACG_COPY_TARGET_RESOURCE_GROUP`: ターゲットリソースグループ名
- `ACG_COPY_TARGET_GALLERY`: ターゲットギャラリー名
- `ACG_COPY_LOG_LEVEL`: ログレベル

**優先順位**: CLIオプション > 環境変数 > 設定ファイル

---

## Configuration File（設定ファイル）

設定ファイルはJSON形式（詳細は `config-schema.json` 参照）
