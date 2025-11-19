using System.CommandLine;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Services.Gallery;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Cli;

/// <summary>
/// イメージコピーコマンド
/// </summary>
public class CopyCommand
{
    private readonly IGalleryClientFactory _clientFactory;
    private readonly IGalleryQueryService _queryService;
    private readonly IGalleryCopyService _copyService;
    private readonly ILogger<CopyCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// CopyCommandのコンストラクタ
    /// </summary>
    public CopyCommand(
        IGalleryClientFactory clientFactory,
        IGalleryQueryService queryService,
        IGalleryCopyService copyService,
        ILogger<CopyCommand> logger,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(copyService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _clientFactory = clientFactory;
        _queryService = queryService;
        _copyService = copyService;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// コマンド定義を作成
    /// </summary>
    public Command CreateCommand()
    {
        var command = new Command("copy", "Copy image definitions and versions from source to target gallery");

        // ソースオプション
        var sourceSubscriptionOption = new Option<string>("--source-subscription", "-ss")
        {
            Description = "Source subscription ID",
            Required = true
        };

        var sourceResourceGroupOption = new Option<string>("--source-resource-group", "-srg")
        {
            Description = "Source resource group name",
            Required = true
        };

        var sourceGalleryOption = new Option<string>("--source-gallery", "-sg")
        {
            Description = "Source gallery name",
            Required = true
        };

        // ターゲットオプション
        var targetSubscriptionOption = new Option<string>("--target-subscription", "-ts")
        {
            Description = "Target subscription ID",
            Required = true
        };

        var targetResourceGroupOption = new Option<string>("--target-resource-group", "-trg")
        {
            Description = "Target resource group name",
            Required = true
        };

        var targetGalleryOption = new Option<string>("--target-gallery", "-tg")
        {
            Description = "Target gallery name",
            Required = true
        };

        // 認証オプション
        var tenantIdOption = new Option<string?>("--tenant-id", "-t")
        {
            Description = "Azure tenant ID (optional, can be read from config or environment)",
            Required = false
        };

        var clientIdOption = new Option<string?>("--client-id")
        {
            Description = "Azure app registration client ID (optional, can be read from config or environment)",
            Required = false
        };

        // フィルタオプション
        var includeImagesOption = new Option<string?>("--include-images", "-ii")
        {
            Description = "Comma-separated patterns for image definitions to include",
            Required = false
        };

        var excludeImagesOption = new Option<string?>("--exclude-images", "-ei")
        {
            Description = "Comma-separated patterns for image definitions to exclude",
            Required = false
        };

        var includeVersionsOption = new Option<string?>("--include-versions", "-iv")
        {
            Description = "Comma-separated patterns for versions to include",
            Required = false
        };

        var excludeVersionsOption = new Option<string?>("--exclude-versions", "-ev")
        {
            Description = "Comma-separated patterns for versions to exclude",
            Required = false
        };

        var matchModeOption = new Option<string>("--match-mode", "-mm")
        {
            Description = "Pattern matching mode: 'prefix' or 'contains'",
            Required = false,
            DefaultValueFactory = _ => "prefix"
        };

        // ドライランオプション
        var dryRunOption = new Option<bool>("--dry-run", "-d")
        {
            Description = "Perform a dry run without making any changes",
            Required = false,
            DefaultValueFactory = _ => false
        };

        // すべてのオプションをコマンドに追加
        command.Add(sourceSubscriptionOption);
        command.Add(sourceResourceGroupOption);
        command.Add(sourceGalleryOption);
        command.Add(targetSubscriptionOption);
        command.Add(targetResourceGroupOption);
        command.Add(targetGalleryOption);
        command.Add(tenantIdOption);
        command.Add(clientIdOption);
        command.Add(includeImagesOption);
        command.Add(excludeImagesOption);
        command.Add(includeVersionsOption);
        command.Add(excludeVersionsOption);
        command.Add(matchModeOption);
        command.Add(dryRunOption);

        // ハンドラーを設定（System.CommandLine beta API uses SetAction）
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var sourceSubscription = parseResult.GetValue(sourceSubscriptionOption) ?? "";
            var sourceResourceGroup = parseResult.GetValue(sourceResourceGroupOption) ?? "";
            var sourceGallery = parseResult.GetValue(sourceGalleryOption) ?? "";
            var targetSubscription = parseResult.GetValue(targetSubscriptionOption) ?? "";
            var targetResourceGroup = parseResult.GetValue(targetResourceGroupOption) ?? "";
            var targetGallery = parseResult.GetValue(targetGalleryOption) ?? "";
            var tenantId = parseResult.GetValue(tenantIdOption);
            var clientId = parseResult.GetValue(clientIdOption);
            var includeImages = parseResult.GetValue(includeImagesOption);
            var excludeImages = parseResult.GetValue(excludeImagesOption);
            var includeVersions = parseResult.GetValue(includeVersionsOption);
            var excludeVersions = parseResult.GetValue(excludeVersionsOption);
            var matchMode = parseResult.GetValue(matchModeOption) ?? "prefix"; // null 安全性確保
            var isDryRun = parseResult.GetValue(dryRunOption);

            await ExecuteAsync(
                sourceSubscription,
                sourceResourceGroup,
                sourceGallery,
                targetSubscription,
                targetResourceGroup,
                targetGallery,
                tenantId,
                clientId,
                includeImages,
                excludeImages,
                includeVersions,
                excludeVersions,
                matchMode,
                isDryRun);
        });

        return command;
    }

    /// <summary>
    /// コマンド実行
    /// </summary>
    private async Task ExecuteAsync(
        string sourceSubscription,
        string sourceResourceGroup,
        string sourceGallery,
        string targetSubscription,
        string targetResourceGroup,
        string targetGallery,
        string? tenantId,
        string? clientId,
        string? includeImages,
        string? excludeImages,
        string? includeVersions,
        string? excludeVersions,
        string matchMode,
        bool dryRun)
    {
        try
        {
            _logger.LogInformation("Starting copy command execution (DryRun: {DryRun})", dryRun);

            // フィルタ基準を構築
            var filterCriteria = new FilterCriteria
            {
                ImageDefinitionIncludes = ParsePatterns(includeImages),
                ImageDefinitionExcludes = ParsePatterns(excludeImages),
                VersionIncludes = ParsePatterns(includeVersions),
                VersionExcludes = ParsePatterns(excludeVersions),
                MatchMode = Enum.Parse<MatchMode>(matchMode, ignoreCase: true)
            };

            // ソースおよびターゲット AzureContext を構築
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new InvalidOperationException("Tenant ID is required (via --tenant-id, environment, or config)");
            }

            var sourceContext = new AzureContext
            {
                TenantId = tenantId,
                SubscriptionId = sourceSubscription,
                ResourceGroupName = sourceResourceGroup,
                GalleryName = sourceGallery
            };

            var targetContext = new AzureContext
            {
                TenantId = tenantId,
                SubscriptionId = targetSubscription,
                ResourceGroupName = targetResourceGroup,
                GalleryName = targetGallery
            };

            // 認証情報を使用してクライアントを作成
            var sourceClient = _clientFactory.CreateArmClient(tenantId, sourceSubscription);
            var targetClient = _clientFactory.CreateArmClient(tenantId, targetSubscription);

            // ギャラリーリソースを取得
            var sourceGalleryCollection = _clientFactory.GetGalleryCollection(sourceContext, sourceClient);
            var targetGalleryCollection = _clientFactory.GetGalleryCollection(targetContext, targetClient);

            var sourceGalleryResponse = await sourceGalleryCollection.GetAsync(sourceGallery);
            var targetGalleryResponse = await targetGalleryCollection.GetAsync(targetGallery);

            var sourceGalleryResource = sourceGalleryResponse.Value;
            var targetGalleryResource = targetGalleryResponse.Value;

            _logger.LogInformation("Source gallery: {SourceGallery}", GetNameFromId(sourceGalleryResource.Id.ToString()));
            _logger.LogInformation("Target gallery: {TargetGallery}", GetNameFromId(targetGalleryResource.Id.ToString()));

            // コピーを実行
            var summary = await _copyService.CopyAllImagesAsync(
                sourceGalleryResource,
                targetGalleryResource,
                sourceContext,
                targetContext,
                filterCriteria,
                dryRun);

            // サマリーを出力
            PrintSummary(summary);

            // 終了コードを決定
            Environment.Exit(DetermineExitCode(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError("Copy command failed: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
            Environment.Exit(4); // 認証エラーまたはその他の予期しないエラー
        }
    }

    /// <summary>
    /// パターン文字列をリストに分割
    /// </summary>
    private List<string> ParsePatterns(string? patternsStr)
    {
        if (string.IsNullOrWhiteSpace(patternsStr))
        {
            return new List<string>();
        }

        return patternsStr
            .Split(',')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
    }

    /// <summary>
    /// サマリーを出力
    /// </summary>
    private void PrintSummary(CopySummary summary)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(summary.IsDryRun ? "Copy Plan Summary" : "Copy Summary");
        Console.WriteLine("========================================");
        Console.WriteLine($"Duration: {summary.EndTime - summary.StartTime:hh\\:mm\\:ss}");
        Console.WriteLine($"Source: Subscription '{summary.SourceContext.SubscriptionId}', " +
            $"Gallery '{summary.SourceContext.GalleryName}'");
        Console.WriteLine($"Target: Subscription '{summary.TargetContext.SubscriptionId}', " +
            $"Gallery '{summary.TargetContext.GalleryName}'");
        Console.WriteLine();
        Console.WriteLine("Results:");
        Console.WriteLine($"  Image Definitions Created: {summary.CreatedImageDefinitions}");
        Console.WriteLine($"  Image Versions Created: {summary.CreatedImageVersions}");
        Console.WriteLine($"  Image Versions Skipped: {summary.SkippedImageVersions}");
        Console.WriteLine($"  Failed Operations: {summary.FailedOperations}");
        Console.WriteLine();

        // スキップされた操作を表示
        var skippedOps = summary.Operations.Where(op => op.Result == OperationResult.Skipped).ToList();
        if (skippedOps.Any())
        {
            Console.WriteLine("Skipped Operations:");
            foreach (var op in skippedOps)
            {
                if (op.Type == OperationType.CreateImageVersion)
                {
                    Console.WriteLine($"  - Version '{op.ImageDefinitionName}/{op.VersionName}': {op.SkipReason}");
                }
                else if (op.Type == OperationType.CreateImageDefinition)
                {
                    Console.WriteLine($"  - Image '{op.ImageDefinitionName}': {op.SkipReason}");
                }
            }
            Console.WriteLine();
        }

        // 失敗した操作を表示
        var failedOps = summary.Operations.Where(op => op.Result == OperationResult.Failed).ToList();
        if (failedOps.Any())
        {
            Console.WriteLine("Failed Operations:");
            foreach (var op in failedOps)
            {
                if (op.Type == OperationType.CreateImageVersion)
                {
                    Console.WriteLine($"  - Version '{op.ImageDefinitionName}/{op.VersionName}': " +
                        $"{op.ErrorMessage} (Code: {op.ErrorCode})");
                }
                else if (op.Type == OperationType.CreateImageDefinition)
                {
                    Console.WriteLine($"  - Image '{op.ImageDefinitionName}': " +
                        $"{op.ErrorMessage} (Code: {op.ErrorCode})");
                }
            }
        }
    }

    /// <summary>
    /// 終了コードを決定
    /// </summary>
    private int DetermineExitCode(CopySummary summary)
    {
        if (summary.FailedOperations > 0)
        {
            return 1; // 部分的失敗
        }

        return 0; // 成功
    }

    /// <summary>
    /// ARM ID 文字列から末尾の名前部分を抽出
    /// </summary>
    private string GetNameFromId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;
        var segments = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? id : segments[^1];
    }
}
