# Data Model: Azure Compute Gallery クロスサブスクリプションコピー

**日付**: 2025-11-17  
**目的**: ツールで使用するデータモデル、エンティティ、およびバリデーションルールの定義

---

## 1. Core Entities（コアエンティティ）

### 1.1 AzureContext（Azureコンテキスト）

**目的**: Azure環境への接続情報を表現

```csharp
public class AzureContext
{
    /// <summary>
    /// テナントID（同一テナント内のクロスサブスクリプション操作に必須）
    /// </summary>
    public string TenantId { get; set; }
    
    /// <summary>
    /// サブスクリプションID
    /// </summary>
    public string SubscriptionId { get; set; }
    
    /// <summary>
    /// リソースグループ名
    /// </summary>
    public string ResourceGroupName { get; set; }
    
    /// <summary>
    /// ギャラリー名
    /// </summary>
    public string GalleryName { get; set; }
}
```

**バリデーションルール**:
- `TenantId`: GUID形式、必須
- `SubscriptionId`: GUID形式、必須
- `ResourceGroupName`: 1-90文字、英数字とハイフン、アンダースコア、ピリオド、括弧のみ、必須
- `GalleryName`: 1-80文字、英数字とピリオドのみ、必須

**状態遷移**: なし（不変オブジェクト）

---

### 1.2 GalleryImageDefinition（イメージ定義）

**目的**: Azure Compute Galleryのイメージ定義を表現

```csharp
public class GalleryImageDefinition
{
    /// <summary>
    /// イメージ定義名
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// OS種別（変更不可能な属性）
    /// </summary>
    public OperatingSystemType OSType { get; set; }
    
    /// <summary>
    /// OS状態（Generalized or Specialized）（変更不可能な属性）
    /// </summary>
    public OperatingSystemStateType OSState { get; set; }
    
    /// <summary>
    /// Hyper-V世代（変更不可能な属性）
    /// </summary>
    public HyperVGeneration HyperVGeneration { get; set; }
    
    /// <summary>
    /// アーキテクチャ（x64, Arm64）（変更不可能な属性）
    /// </summary>
    public Architecture? Architecture { get; set; }
    
    /// <summary>
    /// イメージ識別子（Publisher, Offer, SKU）
    /// </summary>
    public GalleryImageIdentifier Identifier { get; set; }
    
    /// <summary>
    /// セキュリティ機能（例: TrustedLaunch, ConfidentialVM）
    /// </summary>
    public IList<GalleryImageFeature> Features { get; set; }
    
    /// <summary>
    /// イメージバージョンのコレクション
    /// </summary>
    public IList<GalleryImageVersion> Versions { get; set; }
}

public enum OperatingSystemType
{
    Windows,
    Linux
}

public enum OperatingSystemStateType
{
    Generalized,
    Specialized
}

public enum HyperVGeneration
{
    V1,
    V2
}

public enum Architecture
{
    x64,
    Arm64
}
```

**バリデーションルール**:
- `Name`: 1-80文字、英数字とハイフン、アンダースコア、ピリオドのみ、必須
- `OSType`: 必須、変更不可能
- `OSState`: 必須、変更不可能
- `HyperVGeneration`: 必須、変更不可能
- `Architecture`: オプション、変更不可能
- `Identifier`: 必須

**状態遷移**: 
- 作成後、変更不可能な属性（OSType, OSState, HyperVGeneration, Architecture）は変更不可
- Versionsは追加のみ可能、削除は外部操作

**不整合処理**:
- ターゲットに同名定義が存在し、変更不可能な属性が不一致の場合: エラーで中断
- エラーメッセージ例: "Image definition 'ImageName' already exists with incompatible OSType (Source: Linux, Target: Windows)"

---

### 1.3 GalleryImageVersion（イメージバージョン）

**目的**: イメージ定義の特定バージョンを表現

```csharp
public class GalleryImageVersion
{
    /// <summary>
    /// バージョン名（Major.Minor.Patch形式、例: 1.0.0）
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// ソースイメージまたはVMへの参照
    /// </summary>
    public string SourceId { get; set; }
    
    /// <summary>
    /// レプリケーション対象リージョン
    /// </summary>
    public IList<TargetRegion> TargetRegions { get; set; }
    
    /// <summary>
    /// レプリカ数（各リージョン）
    /// </summary>
    public int ReplicaCount { get; set; }
    
    /// <summary>
    /// 最新版から除外するか
    /// </summary>
    public bool ExcludeFromLatest { get; set; }
    
    /// <summary>
    /// サポート終了日
    /// </summary>
    public DateTimeOffset? EndOfLifeOn { get; set; }
    
    /// <summary>
    /// ストレージアカウントタイプ（Standard_LRS, Standard_ZRS等）
    /// </summary>
    public string StorageAccountType { get; set; }
}

public class TargetRegion
{
    /// <summary>
    /// Azureリージョン名（例: eastus, westeurope）
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// リージョナルレプリカ数
    /// </summary>
    public int RegionalReplicaCount { get; set; }
    
    /// <summary>
    /// ストレージアカウントタイプ（リージョンごとに設定可能）
    /// </summary>
    public string StorageAccountType { get; set; }
    
    /// <summary>
    /// 暗号化設定
    /// </summary>
    public EncryptionImages? Encryption { get; set; }
}
```

**バリデーションルール**:
- `Name`: Major.Minor.Patch形式（各部は0-2147483647）、必須
- `SourceId`: Azure Resource ID形式、必須
- `TargetRegions`: 最低1リージョン、必須
- `ReplicaCount`: 1以上、必須
- `StorageAccountType`: Standard_LRS, Standard_ZRS, Premium_LRS のいずれか

**状態遷移**:
- 作成中 → 完了 → 削除（外部操作）
- 作成後は変更不可（Azureの制限）

**リージョン制約処理**:
- ターゲットで利用不可なリージョンが含まれる場合: 当該バージョンをスキップ、ログ出力
- ログメッセージ例: "Skipping version '1.0.0': Target region 'invalidregion' is not available in target subscription"

---

### 1.4 FilterCriteria（フィルタ基準）

**目的**: コピー対象のイメージ定義・バージョンを絞り込む条件

```csharp
public class FilterCriteria
{
    /// <summary>
    /// イメージ定義名のIncludeパターン（前方一致または部分一致）
    /// </summary>
    public IList<string> ImageDefinitionIncludes { get; set; }
    
    /// <summary>
    /// イメージ定義名のExcludeパターン（前方一致または部分一致）
    /// </summary>
    public IList<string> ImageDefinitionExcludes { get; set; }
    
    /// <summary>
    /// バージョン名のIncludeパターン（前方一致または部分一致）
    /// </summary>
    public IList<string> VersionIncludes { get; set; }
    
    /// <summary>
    /// バージョン名のExcludeパターン（前方一致または部分一致）
    /// </summary>
    public IList<string> VersionExcludes { get; set; }
    
    /// <summary>
    /// パターンマッチ方式（Prefix: 前方一致, Contains: 部分一致）
    /// </summary>
    public MatchMode MatchMode { get; set; }
}

public enum MatchMode
{
    /// <summary>
    /// 前方一致（例: "ubuntu" → "ubuntu-20.04", "ubuntu-22.04"）
    /// </summary>
    Prefix,
    
    /// <summary>
    /// 部分一致（例: "ubuntu" → "my-ubuntu-image", "ubuntu-server"）
    /// </summary>
    Contains
}
```

**バリデーションルール**:
- `ImageDefinitionIncludes`, `ImageDefinitionExcludes`: オプション（空の場合はフィルタなし）
- `VersionIncludes`, `VersionExcludes`: オプション（空の場合はフィルタなし）
- `MatchMode`: 必須、デフォルトは`Prefix`

**フィルタ適用ルール**:
1. Includeが指定されている場合: パターンに一致するもののみ対象
2. Excludeが指定されている場合: パターンに一致するものを除外
3. 両方指定されている場合: Include適用後にExcludeを適用
4. 何も指定されていない場合: すべてが対象

**状態遷移**: なし（設定オブジェクト）

---

### 1.5 CopyOperation（コピー操作）

**目的**: 単一のイメージ定義またはバージョンのコピー操作を表現

```csharp
public class CopyOperation
{
    /// <summary>
    /// 操作ID（ログ相関用）
    /// </summary>
    public Guid OperationId { get; set; }
    
    /// <summary>
    /// 操作種別
    /// </summary>
    public OperationType Type { get; set; }
    
    /// <summary>
    /// 対象イメージ定義名
    /// </summary>
    public string ImageDefinitionName { get; set; }
    
    /// <summary>
    /// 対象バージョン名（バージョンコピーの場合のみ）
    /// </summary>
    public string VersionName { get; set; }
    
    /// <summary>
    /// 操作結果
    /// </summary>
    public OperationResult Result { get; set; }
    
    /// <summary>
    /// スキップ理由（Result=Skippedの場合）
    /// </summary>
    public string SkipReason { get; set; }
    
    /// <summary>
    /// エラーメッセージ（Result=Failedの場合）
    /// </summary>
    public string ErrorMessage { get; set; }
    
    /// <summary>
    /// エラーコード（Result=Failedの場合、AzureエラーコードまたはHTTPステータス）
    /// </summary>
    public string ErrorCode { get; set; }
    
    /// <summary>
    /// 操作開始時刻
    /// </summary>
    public DateTimeOffset StartTime { get; set; }
    
    /// <summary>
    /// 操作終了時刻
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }
}

public enum OperationType
{
    /// <summary>
    /// ギャラリーの作成
    /// </summary>
    CreateGallery,
    
    /// <summary>
    /// イメージ定義の作成
    /// </summary>
    CreateImageDefinition,
    
    /// <summary>
    /// イメージバージョンの作成
    /// </summary>
    CreateImageVersion
}

public enum OperationResult
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,
    
    /// <summary>
    /// スキップ（既存または制約による）
    /// </summary>
    Skipped,
    
    /// <summary>
    /// 失敗
    /// </summary>
    Failed
}
```

**バリデーションルール**:
- `OperationId`: 必須、一意
- `Type`: 必須
- `ImageDefinitionName`: 必須
- `VersionName`: Type=CreateImageVersionの場合必須
- `Result`: 必須
- `SkipReason`: Result=Skippedの場合必須
- `ErrorMessage`, `ErrorCode`: Result=Failedの場合必須

**状態遷移**:
```
Created → Running → (Success | Skipped | Failed)
```

---

### 1.6 CopySummary（コピーサマリー）

**目的**: コピー操作全体の結果サマリー

```csharp
public class CopySummary
{
    /// <summary>
    /// 操作開始時刻
    /// </summary>
    public DateTimeOffset StartTime { get; set; }
    
    /// <summary>
    /// 操作終了時刻
    /// </summary>
    public DateTimeOffset EndTime { get; set; }
    
    /// <summary>
    /// ソースコンテキスト
    /// </summary>
    public AzureContext SourceContext { get; set; }
    
    /// <summary>
    /// ターゲットコンテキスト
    /// </summary>
    public AzureContext TargetContext { get; set; }
    
    /// <summary>
    /// 全操作のリスト
    /// </summary>
    public IList<CopyOperation> Operations { get; set; }
    
    /// <summary>
    /// 作成されたイメージ定義数
    /// </summary>
    public int CreatedImageDefinitions { get; set; }
    
    /// <summary>
    /// 作成されたイメージバージョン数
    /// </summary>
    public int CreatedImageVersions { get; set; }
    
    /// <summary>
    /// スキップされたイメージバージョン数
    /// </summary>
    public int SkippedImageVersions { get; set; }
    
    /// <summary>
    /// 失敗した操作数
    /// </summary>
    public int FailedOperations { get; set; }
    
    /// <summary>
    /// ドライランモードか
    /// </summary>
    public bool IsDryRun { get; set; }
}
```

**バリデーションルール**:
- すべてのプロパティは必須
- `CreatedImageDefinitions`, `CreatedImageVersions`, `SkippedImageVersions`, `FailedOperations`: 0以上

**状態遷移**: なし（結果オブジェクト）

---

## 2. Configuration Models（設定モデル）

### 2.1 ToolConfiguration（ツール設定）

**目的**: appsettings.json、環境変数、CLIオプションからの設定

```csharp
public class ToolConfiguration
{
    /// <summary>
    /// ソースのAzureコンテキスト
    /// </summary>
    public AzureContext Source { get; set; }
    
    /// <summary>
    /// ターゲットのAzureコンテキスト
    /// </summary>
    public AzureContext Target { get; set; }
    
    /// <summary>
    /// フィルタ基準
    /// </summary>
    public FilterCriteria Filter { get; set; }
    
    /// <summary>
    /// ドライランモード
    /// </summary>
    public bool DryRun { get; set; }
    
    /// <summary>
    /// ログレベル（Trace, Debug, Information, Warning, Error, Critical）
    /// </summary>
    public string LogLevel { get; set; }
    
    /// <summary>
    /// 認証設定
    /// </summary>
    public AuthenticationConfiguration Authentication { get; set; }
}

public class AuthenticationConfiguration
{
    /// <summary>
    /// テナントID（ソースとターゲットで共通）
    /// </summary>
    public string TenantId { get; set; }
    
    /// <summary>
    /// アプリ登録のクライアントID
    /// </summary>
    public string ClientId { get; set; }
}
```

**バリデーションルール**:
- `Source`, `Target`: 必須
- `Source.TenantId` == `Target.TenantId`: 必須（同一テナント制約）
- `DryRun`: デフォルトfalse
- `LogLevel`: "Trace", "Debug", "Information", "Warning", "Error", "Critical"のいずれか、デフォルト"Information"
- `Authentication`: 必須

**設定優先順位**:
1. CLIオプション（最優先）
2. 環境変数
3. appsettings.json（最低優先）

---

## 3. Validation Summary（バリデーションサマリー）

### 共通バリデーション戦略

1. **Null/空チェック**: すべての必須フィールドでNull/空チェック
2. **形式チェック**: GUID、リソース名、バージョン名の形式チェック
3. **範囲チェック**: 数値（レプリカ数等）の範囲チェック
4. **整合性チェック**: 
   - ソースとターゲットのテナントIDが同一
   - イメージ定義の変更不可能な属性の一致
   - リージョン名の有効性

### バリデーションタイミング

1. **設定読み込み時**: ToolConfigurationのバリデーション
2. **操作前**: AzureContextのリソース存在チェック
3. **コピー前**: イメージ定義の変更不可能な属性の一致チェック
4. **バージョン作成前**: ターゲットリージョンの有効性チェック

### エラーレスポンス

- バリデーションエラー時は即座に終了（部分実行なし）
- エラーメッセージには以下を含む:
  - どのフィールドが問題か
  - 期待される値
  - 実際の値（該当する場合）
  - 修正方法の提案

**例**:
```
Error: Source and target must be in the same tenant.
  Source TenantId: 12345678-1234-1234-1234-123456789012
  Target TenantId: 87654321-4321-4321-4321-210987654321
  
  To fix: Ensure both subscriptions belong to the same Azure AD tenant.
```

---

## 4. Entity Relationships（エンティティ関係）

```
ToolConfiguration
├── Source: AzureContext
│   └── GalleryName → Gallery
│       └── ImageDefinitions: List<GalleryImageDefinition>
│           └── Versions: List<GalleryImageVersion>
│               └── TargetRegions: List<TargetRegion>
├── Target: AzureContext
│   └── GalleryName → Gallery (target)
├── Filter: FilterCriteria
└── Authentication: AuthenticationConfiguration

CopySummary
├── SourceContext: AzureContext
├── TargetContext: AzureContext
└── Operations: List<CopyOperation>
    └── Result: OperationResult
```

---

## 5. Immutability and State（不変性と状態）

### 不変オブジェクト
- `AzureContext`: 作成後変更不可
- `FilterCriteria`: 作成後変更不可
- `ToolConfiguration`: 作成後変更不可

### 状態遷移を持つオブジェクト
- `CopyOperation`: Created → Running → (Success | Skipped | Failed)
- `GalleryImageVersion`: 作成中 → 完了（Azureが管理）

### ミュータブルコレクション
- `GalleryImageDefinition.Versions`: バージョン追加可能
- `CopySummary.Operations`: 操作追加可能

---

## 6. Error Handling（エラーハンドリング）

### エラーカテゴリ

1. **設定エラー**: バリデーション失敗、不正な設定値 → 即座に終了
2. **認証エラー**: 認証失敗、権限不足 → 即座に終了
3. **リソース不在エラー**: ソースリソース不在 → 即座に終了
4. **制約エラー**: 変更不可能な属性不一致、リージョン利用不可 → 当該操作スキップ、他は継続
5. **Azure APIエラー**: レート制限、一時的エラー → ログ出力、終了（再実行推奨）
6. **予期しないエラー**: 例外 → ログ出力、終了

### エラーログ必須項目

すべてのエラーログには以下を含む:
- 操作ID（OperationId）
- 対象リソースID（FullResourceId）
- 操作種別（OperationType）
- HTTPステータス（該当する場合）
- Azureエラーコード（該当する場合）
- エラーメッセージ
- Exception.ToString()（例外の場合）

---

## 次のステップ

Phase 1継続:
- contracts/ の生成（CLI interface schema、config schema）
- quickstart.md の生成（使用者向けドキュメント）
- agent contextの更新
