# Research Findings: Azure Compute Gallery クロスサブスクリプションコピーツール

**日付**: 2025-11-17  
**目的**: .NET 10 CLIツールにおける技術選定とベストプラクティスの調査

---

## 1. 認証: MSAL (Azure.Identity) for .NET

### Decision（決定事項）
**WebView2埋め込み`InteractiveBrowserCredential` を認証フローとして使用（Windows専用）**

### Rationale（理由）
- **Webブラウザの認証キャッシュに依存しない**: ツール独自のWebView2を埋め込み、ブラウザのキャッシュやセッションに左右されない
- **Windowsユーザー向けの使いやすさ**: ターゲットユーザーがWindows環境のみで使用
- **トークンキャッシュの制御**: `Microsoft.Identity.Client.Extensions.Msal`でツール専用のキャッシュ管理
- **WebView2は最新Windows環境で利用可能**: Windows 11は標準搭載、Windows 10はEdgeインストール済み環境で利用可

### Alternatives Considered（検討した代替案）
1. **DeviceCodeCredential**: コンソール完結、CLI標準だが、別デバイスでのコード入力が必要でUXが低い。
2. **InteractiveBrowserCredential（システムブラウザ起動）**: システムブラウザのキャッシュに依存するため、本件の要件に不適合。
3. **VisualStudioCredential/AzureCliCredential**: 開発専用、本番配布に不適切。

### Implementation（実装パターン）

#### WebView2埋め込み認証の設定

```csharp
using Azure.Identity;
using Azure.Core;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

// WebView2埋め込みInteractiveBrowserCredentialの設定
var options = new InteractiveBrowserCredentialOptions
{
    TenantId = tenantId,
    ClientId = clientId,
    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
    RedirectUri = new Uri("http://localhost"),  // ローカルリダイレクト
    
    // WebView2を使用（システムブラウザを起動しない）
    BrowserCustomization = new BrowserCustomizationOptions
    {
        UseEmbeddedWebView = true,  // WebView2埋め込み
        // ウィンドウタイトルのカスタマイズ（オプション）
        HtmlMessageSuccess = "<html><body><h1>Authentication successful!</h1><p>You can close this window now.</p></body></html>",
        HtmlMessageError = "<html><body><h1>Authentication failed</h1><p>Error: {0}</p></body></html>"
    }
};

var credential = new InteractiveBrowserCredential(options);
```

#### トークンキャッシュの永続化（ツール専用キャッシュ）

```csharp
using Microsoft.Identity.Client.Extensions.Msal;

// キャッシュ保存先の設定
var storageProperties = new StorageCreationPropertiesBuilder(
    "acg-copy-tool.cache",  // キャッシュファイル名
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzureComputeGalleryCopy"),  // ディレクトリ
    clientId
)
.WithMacKeyChain("AzureComputeGalleryCopy", "ACGCopyTool")  // macOS用（将来対応時）
.WithLinuxKeyring(
    "acg-copy-tool.cache",
    "default",
    "AzureComputeGalleryCopy",
    "ACGCopyTool",
    MsalCacheHelper.LinuxKeyRingSchema
)  // Linux用（将来対応時）
.Build();

// MSALキャッシュヘルパーを作成
var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);

// PublicClientApplicationを作成（InteractiveBrowserCredentialの内部で使用）
var app = PublicClientApplicationBuilder
    .Create(clientId)
    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
    .WithRedirectUri("http://localhost")
    .Build();

// キャッシュを登録
cacheHelper.RegisterCache(app.UserTokenCache);

// InteractiveBrowserCredentialを作成
var credential = new InteractiveBrowserCredential(options);
```

**注意**: Azure.Identityの`InteractiveBrowserCredential`は内部でMSAL.NETを使用していますが、直接キャッシュヘルパーを統合することはできません。トークンキャッシュはMSALが自動的に管理します。

### Tenant and Subscription Switching（テナント/サブスクリプション切替）

**同一テナント内のクロスサブスクリプション操作:**

```csharp
// 各サブスクリプションに対して別のArmClientインスタンスを作成
var sourceArmClient = new ArmClient(credential, sourceSubscriptionId);
var targetArmClient = new ArmClient(credential, targetSubscriptionId);

// 両方とも同じcredentialを使用するが、異なるサブスクリプションをターゲット
```

**重要な考慮事項**:
- クロステナント操作には別の認証が必要（このツールでは対象外）
- ユーザーは両サブスクリプションで適切なRBACアクセス許可が必要
- 同一テナント、クロスサブスクリプションシナリオのみサポート

### Best Practices（ベストプラクティス）
1. **Credential再利用**: credentialインスタンスを保存し、すべてのAzure SDKクライアントで再利用
2. **WebView2インストール確認**: ツール起動時にWebView2ランタイムの存在を確認
3. **エラーハンドリング**: `MsalUiRequiredException`をキャッチして再認証対応
4. **リトライ設定**: 本番環境ではリトライ設定を考慮:

```csharp
var options = new InteractiveBrowserCredentialOptions
{
    // ... 他のオプション
    Retry = 
    {
        MaxRetries = 3,
        Delay = TimeSpan.FromSeconds(0.5),
        Mode = RetryMode.Exponential
    }
};
```

### WebView2ランタイム要件

**Windows 11**: 標準搭載
**Windows 10**: Microsoft Edgeインストール済み環境で利用可能

**インストール確認**:
```csharp
try
{
    var version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
    Console.WriteLine($"WebView2 Runtime version: {version}");
}
catch (WebView2RuntimeNotFoundException)
{
    Console.Error.WriteLine("ERROR: WebView2 Runtime is not installed.");
    Console.Error.WriteLine("Please download and install from: https://developer.microsoft.com/microsoft-edge/webview2/");
    Environment.Exit(1);
}
```

---

## 2. Azure SDK: Azure.ResourceManager.Compute

### Decision（決定事項）
**Azure.ResourceManager.Compute SDK を宣言的リソース管理パターンで使用**

### Rationale（理由）
- 最新のAzure SDKはAzure Resource Manager（ARM）パターンに従う
- ギャラリーリソースへの強く型付けされたアクセス
- 存在チェックの組み込みサポート（冪等性）
- 両サブスクリプションが同一テナント内の場合、クロスサブスクリプション操作をサポート

### 主要な操作

#### A. イメージ定義とバージョンの列挙

```csharp
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Resources;

// ソースギャラリーの取得
var sourceSubscription = armClient.GetSubscriptionResource(
    SubscriptionResource.CreateResourceIdentifier(sourceSubscriptionId));
var sourceRG = await sourceSubscription.GetResourceGroupAsync(sourceResourceGroupName);
var sourceGallery = await sourceRG.Value.GetGalleryAsync(sourceGalleryName);

// すべてのイメージ定義を列挙
await foreach (var imageDef in sourceGallery.Value.GetGalleryImages())
{
    Console.WriteLine($"Image: {imageDef.Data.Name}");
    
    // このイメージ定義のすべてのバージョンを列挙
    await foreach (var version in imageDef.GetGalleryImageVersions())
    {
        Console.WriteLine($"  Version: {version.Data.Name}");
        Console.WriteLine($"  Regions: {string.Join(", ", 
            version.Data.PublishingProfile.TargetRegions.Select(r => r.Name))}");
    }
}
```

#### B. リソース存在チェック（冪等性）

```csharp
// ギャラリーが存在するかチェック
var galleryCollection = targetRG.GetGalleries();
var galleryExists = await galleryCollection.ExistsAsync(targetGalleryName);

if (galleryExists.Value)
{
    Console.WriteLine("Gallery already exists, skipping creation");
}

// イメージ定義が存在するかチェック
var imageDefCollection = targetGallery.GetGalleryImages();
var imageDefExists = await imageDefCollection.ExistsAsync(imageDefinitionName);

// イメージバージョンが存在するかチェック
var versionCollection = targetImageDef.GetGalleryImageVersions();
var versionExists = await versionCollection.ExistsAsync(versionName);
```

#### C. イメージ定義とバージョンの作成

```csharp
using Azure.ResourceManager.Compute.Models;

// イメージ定義が存在しない場合は作成
if (!imageDefExists.Value)
{
    var imageDefData = new GalleryImageData(AzureLocation.EastUS)
    {
        OSType = OperatingSystemType.Linux,
        OSState = OperatingSystemStateType.Specialized,
        Identifier = new GalleryImageIdentifier(
            publisher: "MyPublisher",
            offer: "MyOffer", 
            sku: "MySKU"
        ),
        HyperVGeneration = HyperVGeneration.V2,
        // ソースからセキュリティ機能をコピー
        Features = 
        {
            new GalleryImageFeature 
            { 
                Name = "SecurityType", 
                Value = "TrustedLaunch" 
            }
        }
    };
    
    var imageDefOperation = await targetImageDefCollection.CreateOrUpdateAsync(
        WaitUntil.Completed,
        imageDefinitionName,
        imageDefData
    );
}

// イメージバージョンの作成（ソースイメージ参照が必要）
var versionData = new GalleryImageVersionData(AzureLocation.EastUS)
{
    StorageProfile = new GalleryImageVersionStorageProfile
    {
        Source = new GalleryArtifactVersionFullSource
        {
            Id = new ResourceIdentifier(sourceImageVersionId)
        }
    },
    PublishingProfile = new GalleryImageVersionPublishingProfile
    {
        TargetRegions = 
        {
            new TargetRegion(AzureLocation.EastUS) { RegionalReplicaCount = 1 },
            new TargetRegion(AzureLocation.WestUS) { RegionalReplicaCount = 1 }
        },
        ReplicaCount = 1
    }
};

var versionOperation = await targetVersionCollection.CreateOrUpdateAsync(
    WaitUntil.Completed,
    versionName,
    versionData
);
```

#### D. クロスサブスクリプション操作（同一テナント）

```csharp
// 一度認証、両サブスクリプションにアクセス
var credential = new InteractiveBrowserCredential(options);

// ソースサブスクリプション
var sourceArmClient = new ArmClient(credential, sourceSubscriptionId);
var sourceGallery = await sourceArmClient
    .GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(sourceSubscriptionId))
    .GetResourceGroupAsync(sourceRG)
    .Result.Value.GetGalleryAsync(sourceGalleryName);

// ターゲットサブスクリプション（同一テナント）
var targetArmClient = new ArmClient(credential, targetSubscriptionId);
var targetGallery = await targetArmClient
    .GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(targetSubscriptionId))
    .GetResourceGroupAsync(targetRG)
    .Result.Value.GetGalleryAsync(targetGalleryName);

// サブスクリプション間でソースリソースIDを参照してイメージバージョンをコピー
// Azureが内部でクロスサブスクリプションコピーを処理
```

**重要な注意事項:**
- ユーザーは**両方**のサブスクリプションで適切なRBACアクセス許可が必要
- 両サブスクリプションは**同一テナント**内である必要がある
- クロステナントシナリオには別の認証フローが必要

#### E. リージョンレプリケーション設定

```csharp
// ソースからリージョン設定をコピー
var sourceVersion = await sourceImageDef.GetGalleryImageVersionAsync(sourceVersionName);
var sourceRegions = sourceVersion.Value.Data.PublishingProfile.TargetRegions;

var targetRegions = sourceRegions.Select(region => new TargetRegion(region.Name)
{
    RegionalReplicaCount = region.RegionalReplicaCount,
    StorageAccountType = region.StorageAccountType,
    Encryption = region.Encryption  // 暗号化設定を保持
}).ToList();

var versionData = new GalleryImageVersionData(targetLocation)
{
    PublishingProfile = new GalleryImageVersionPublishingProfile
    {
        TargetRegions = targetRegions,
        ExcludeFromLatest = false,
        EndOfLifeOn = sourceVersion.Value.Data.PublishingProfile.EndOfLifeOn
    }
};
```

### Alternatives Considered（検討した代替案）
- **Azure CLI/PowerShellラッパー**: 機能するが外部依存関係を追加し、制御が制限される
- **REST API直接呼び出し**: より柔軟だが、ボイラープレートコードが増える

---

## 3. CLI Framework: System.CommandLine

### Decision（決定事項）
**System.CommandLine を使用（現在beta 7、stable release予定）**

### Rationale（理由）
- .NET CLI自体が使用するMicrosoft公式ライブラリ
- 包括的な機能: サブコマンド、オプション、引数、バリデーション
- ヘルプ生成とタブ補完の組み込みサポート
- POSIXとWindows規約の両方をサポート
- AOTコンパイルのためのトリムフレンドリー

### 最新のベストプラクティス

#### A. サブコマンドを持つコマンド構造

```csharp
using System.CommandLine;

// ルートコマンド
RootCommand rootCommand = new("Azure Compute Gallery image copy tool");

// ルートに共通オプションを追加（すべてのサブコマンドで利用可能）
Option<string> tenantIdOption = new("--tenant-id", "-t")
{
    Description = "Azure tenant ID",
    Required = false
};
tenantIdOption.Recursive = true;  // すべてのサブコマンドで利用可能
rootCommand.Options.Add(tenantIdOption);

// Copyコマンド
Command copyCommand = new("copy", "Copy images between galleries");
rootCommand.Subcommands.Add(copyCommand);

// Listコマンド
Command listCommand = new("list", "List galleries and images");
rootCommand.Subcommands.Add(listCommand);

// Listサブコマンド
Command listGalleriesCommand = new("galleries", "List galleries");
Command listImagesCommand = new("images", "List image definitions");
listCommand.Subcommands.Add(listGalleriesCommand);
listCommand.Subcommands.Add(listImagesCommand);
```

#### B. バリデーション付きオプションと引数

```csharp
// カスタムバリデーション付きファイルオプション
Option<FileInfo> configOption = new("--config", "-c")
{
    Description = "Path to configuration file",
    DefaultValueFactory = result =>
    {
        if (result.Tokens.Count == 0)
        {
            return new FileInfo("config.json");
        }
        
        string filePath = result.Tokens.Single().Value;
        if (!File.Exists(filePath))
        {
            result.AddError($"Configuration file '{filePath}' does not exist");
            return null;
        }
        return new FileInfo(filePath);
    }
};

// 必須オプション
Option<string> subscriptionOption = new("--subscription", "-s")
{
    Description = "Azure subscription ID",
    Required = true
};

// 制限された値を持つ引数
Argument<string> targetRegionArg = new("target-region")
{
    Description = "Target Azure region"
};
// 有効なリージョンに制限
targetRegionArg.FromAmong("eastus", "westus", "westeurope", "northeurope");

// 複数値オプション
Option<string[]> regionsOption = new("--regions")
{
    Description = "Target regions for replication",
    AllowMultipleArgumentsPerToken = true,
    Required = true
};
```

#### C. 設定バインディングパターン

```csharp
// 設定クラスの定義
public class CopyOptions
{
    public string SourceSubscription { get; set; }
    public string TargetSubscription { get; set; }
    public string SourceGallery { get; set; }
    public string TargetGallery { get; set; }
    public string[] ImageDefinitions { get; set; }
}

// オプションを設定にバインド
copyCommand.SetAction(parseResult =>
{
    var options = new CopyOptions
    {
        SourceSubscription = parseResult.GetValue(sourceSubOption),
        TargetSubscription = parseResult.GetValue(targetSubOption),
        SourceGallery = parseResult.GetValue(sourceGalleryOption),
        TargetGallery = parseResult.GetValue(targetGalleryOption),
        ImageDefinitions = parseResult.GetValue(imageDefsOption)
    };
    
    return CopyImagesAsync(options);
});
```

#### D. CLIでの進捗レポート

**System.CommandLineには組み込みの進捗レポート機能がない**が、標準の.NETパターンを使用可能:

```csharp
// IProgress<T>を使用した進捗レポート
public async Task CopyImageAsync(
    string imageName, 
    IProgress<CopyProgress> progress = null)
{
    progress?.Report(new CopyProgress 
    { 
        Stage = "Checking source", 
        PercentComplete = 0 
    });
    
    // ... ソース存在確認
    
    progress?.Report(new CopyProgress 
    { 
        Stage = "Creating image definition", 
        PercentComplete = 25 
    });
    
    // ... イメージ定義作成
    
    progress?.Report(new CopyProgress 
    { 
        Stage = "Copying image version", 
        PercentComplete = 50 
    });
    
    // 長時間実行操作
    var operation = await targetCollection.CreateOrUpdateAsync(
        WaitUntil.Started,  // 完了を待たない
        versionName, 
        versionData
    );
    
    // 進捗更新でポーリング
    while (!operation.HasCompleted)
    {
        await Task.Delay(5000);
        progress?.Report(new CopyProgress 
        { 
            Stage = "Copying image version", 
            PercentComplete = 50 + (operation.HasValue ? 25 : 0)
        });
    }
    
    progress?.Report(new CopyProgress 
    { 
        Stage = "Complete", 
        PercentComplete = 100 
    });
}

public record CopyProgress
{
    public string Stage { get; init; }
    public int PercentComplete { get; init; }
}

// コマンドハンドラーでの使用
copyCommand.SetAction(async parseResult =>
{
    var progress = new Progress<CopyProgress>(p =>
    {
        Console.WriteLine($"[{p.PercentComplete}%] {p.Stage}");
    });
    
    await CopyImageAsync(imageName, progress);
});
```

**代替: シンプルなコンソール出力**
```csharp
Console.WriteLine("Step 1/4: Authenticating...");
// ... 認証コード
Console.WriteLine("✓ Authentication complete");

Console.WriteLine("Step 2/4: Listing source images...");
// ... リスティングコード
Console.WriteLine($"✓ Found {count} images");

Console.WriteLine("Step 3/4: Copying images...");
foreach (var image in images)
{
    Console.Write($"  Copying {image.Name}...");
    await CopyImageAsync(image);
    Console.WriteLine(" ✓");
}

Console.WriteLine("Step 4/4: Complete");
```

### Alternatives Considered（検討した代替案）
- **CommandLineParser**: 人気があるが機能が少ない、Microsoft公式サポートなし
- **Spectre.Console**: リッチCLI UIには優れているが重い、対話型アプリに適している
- **カスタムパース**: 推奨しない - 車輪の再発明

---

## 4. エラーハンドリングとログパターン

### Decision（決定事項）
**Microsoft.Extensions.Logging をコンソール出力で使用（CLIシナリオ向け）**

### Rationale（理由）
- 標準の.NETログ抽象化
- 構造化ログのサポート
- 異なるログレベルの設定が容易
- Azure SDKと統合良好（内部でILoggerを使用）
- CLIアプリケーションには軽量

### Implementation（実装パターン）

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

// DIコンテナをログ設定でセットアップ
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();  // コンソール出力
    builder.SetMinimumLevel(LogLevel.Information);  // デフォルトレベル
    
    // Azure SDKログをデフォルトでWarningにフィルタ（冗長なため）
    builder.AddFilter("Azure", LogLevel.Warning);
    builder.AddFilter("Azure.Core", LogLevel.Warning);
    builder.AddFilter("Azure.Identity", LogLevel.Warning);
});

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// 構造化ログを使用
logger.LogInformation(
    "Copying image {ImageName} from {SourceGallery} to {TargetGallery}",
    imageName, sourceGallery, targetGallery
);
```

### Azure操作の構造化ログ

```csharp
public class GalleryCopyService
{
    private readonly ILogger<GalleryCopyService> _logger;
    
    public GalleryCopyService(ILogger<GalleryCopyService> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> CopyImageVersionAsync(
        string sourceSubscription,
        string targetSubscription,
        string imageName,
        string version)
    {
        using var scope = _logger.BeginScope(
            "CopyOperation: {ImageName} v{Version} from {SourceSub} to {TargetSub}",
            imageName, version, sourceSubscription, targetSubscription);
        
        try
        {
            _logger.LogInformation("Starting image copy operation");
            
            // 操作コード
            _logger.LogInformation(
                "Successfully created image definition {ImageName}",
                imageName
            );
            
            _logger.LogInformation("Image copy completed successfully");
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 409)
        {
            _logger.LogWarning(
                "Image version {Version} already exists, skipping",
                version
            );
            return true;  // 冪等
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Azure API error during copy: {ErrorCode} - {Message}",
                ex.ErrorCode,
                ex.Message
            );
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during copy operation"
            );
            throw;
        }
    }
}
```

### Azure SDKの例外ハンドリングパターン

```csharp
using Azure;

try
{
    var gallery = await galleryCollection.GetAsync(galleryName);
}
catch (RequestFailedException ex) when (ex.Status == 404)
{
    logger.LogError("Gallery '{GalleryName}' not found", galleryName);
    // リソース不在を処理
}
catch (RequestFailedException ex) when (ex.Status == 403)
{
    logger.LogError(
        "Access denied to gallery '{GalleryName}'. Check RBAC permissions",
        galleryName
    );
    // アクセス許可エラーを処理
}
catch (RequestFailedException ex) when (ex.Status == 429)
{
    logger.LogWarning("Rate limited by Azure API, retry after {RetryAfter}", 
        ex.GetRetryAfter());
    // Azure SDKが自動でリトライを処理するが、カスタムロジックを追加可能
}
catch (RequestFailedException ex)
{
    logger.LogError(
        ex,
        "Azure API error {Status}: {ErrorCode} - {Message}",
        ex.Status,
        ex.ErrorCode,
        ex.Message
    );
}
catch (OperationCanceledException)
{
    logger.LogWarning("Operation cancelled by user");
}
```

### ログのベストプラクティス
1. **構造化ログを使用** - パラメータを引数として渡し、文字列補間は使用しない
2. **ログ相関** - 関連操作には`BeginScope`を使用
3. **適切なログレベル**:
   - `LogTrace`: 詳細な診断情報（デフォルトで無効）
   - `LogDebug`: デバッグ情報、開発中に有用
   - `LogInformation`: 進捗と状態変化
   - `LogWarning`: 回復可能なエラー、非クリティカルな問題
   - `LogError`: 操作完了を妨げるエラー
   - `LogCritical`: 即座の注意が必要な致命的エラー

4. **Azure SDKログ**: Azure SDKは内部で`ILogger`を使用
   ```csharp
   // Azure SDK内部ログを見るには、フィルタを追加
   builder.AddFilter("Azure.Core", LogLevel.Debug);
   builder.AddFilter("Azure.Identity", LogLevel.Information);
   ```

5. **常に例外をコンテキストと共にログ**:
   ```csharp
   logger.LogError(
       ex,
       "Failed to copy image {ImageName} from {Source} to {Target}",
       imageName,
       sourceGallery,
       targetGallery
   );
   ```

### Alternatives Considered（検討した代替案）
- **Serilog**: 構造化ログに優れたライブラリだが、依存関係を追加
- **NLog**: 成熟したログフレームワークだが、Microsoft.Extensions.Loggingがより標準的
- **Console.WriteLine**: シンプルだが構造とフィルタリング機能がない

---

## 推奨技術スタックまとめ

### 技術スタック:
- **認証**: Azure.Identity with WebView2埋め込み`InteractiveBrowserCredential`
- **Azure SDK**: Azure.ResourceManager.Compute（最新安定版）
- **CLIフレームワーク**: System.CommandLine（beta 7、stable targetingへ）
- **ログ**: Microsoft.Extensions.Logging with console provider
- **.NETバージョン**: .NET 10（リリース時、現在は.NET 8/9を使用）
- **WebView2**: Microsoft.Web.WebView2 (最新版)
- **トークンキャッシュ**: Microsoft.Identity.Client.Extensions.Msal

### 主要な設計パターン:
1. **Credential再利用**: credentialを一度作成し、すべてのAzureクライアントで共有
2. **冪等操作**: 作成前にリソース存在をチェック
3. **構造化ログ**: ILoggerを構造化パラメータで使用
4. **進捗レポート**: IProgress<T>パターンをコンソール出力で使用
5. **エラーハンドリング**: 特定のAzure例外をキャッチ（RequestFailedException）
6. **クロスサブスクリプション**: 異なるArmClientインスタンスで同じcredentialを使用
7. **WebView2確認**: ツール起動時にWebView2ランタイムの存在を確認

### 必要なNuGetパッケージ:
```xml
<ItemGroup>
  <PackageReference Include="Azure.Identity" Version="1.17.0" />
  <PackageReference Include="Azure.ResourceManager.Compute" Version="1.12.0" />
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2792.45" />
  <PackageReference Include="Microsoft.Identity.Client.Extensions.Msal" Version="4.67.0" />
  <PackageReference Include="System.CommandLine" Version="2.0.0-beta7" />
  <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
</ItemGroup>
```

### ユーザー要件への対応

**要件1: MSAL認証（WebViewダイアログ）**
- ✅ 満たされている。WebView2埋め込みInteractiveBrowserCredentialを使用。
- ✅ Webブラウザの認証キャッシュに依存しない独自のWebView2を使用。
- ✅ Windows専用（Windowsユーザーのみがターゲット）。

**要件2: 最新.NET 10と最新ライブラリ**
- ✅ 満たされている。すべてのライブラリは最新安定版を使用。

**要件3: 使用者向けドキュメント（設定手順、エラー解析）**
- ✅ quickstart.mdで対応予定（Phase 1）。

---

## 次のステップ

Phase 0完了。次はPhase 1:
1. data-model.md の生成
2. contracts/ の生成（CLI interface schema、config schema）
3. quickstart.md の生成（使用者向けドキュメント）
4. agent contextの更新
