using System.CommandLine;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Services.Gallery;
using AzureComputeGalleryCopy.Logging;
using AzureComputeGalleryCopy.Cli.Output;
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
    private readonly IOperationLogger _operationLogger;
    private readonly SummaryPrinter _summaryPrinter;
    private readonly DryRunPrinter _dryRunPrinter;

    /// <summary>
    /// CopyCommandのコンストラクタ
    /// </summary>
    public CopyCommand(
        IGalleryClientFactory clientFactory,
        IGalleryQueryService queryService,
        IGalleryCopyService copyService,
        ILogger<CopyCommand> logger,
        ILoggerFactory loggerFactory,
        IOperationLogger operationLogger,
        SummaryPrinter summaryPrinter,
        DryRunPrinter dryRunPrinter)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(copyService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(operationLogger);
        ArgumentNullException.ThrowIfNull(summaryPrinter);
        ArgumentNullException.ThrowIfNull(dryRunPrinter);

        _clientFactory = clientFactory;
        _queryService = queryService;
        _copyService = copyService;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _operationLogger = operationLogger;
        _summaryPrinter = summaryPrinter;
        _dryRunPrinter = dryRunPrinter;
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
        var tenantIdOption = new Option<string>("--tenant-id", "-t")
        {
            Description = "Azure tenant ID (required for authentication)",
            Required = true
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
            var tenantId = parseResult.GetValue(tenantIdOption) ?? "";
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
        string tenantId,
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

            var operationId = Guid.NewGuid().ToString();
            var commandMetadata = new Dictionary<string, string>
            {
                { "Command", "copy" },
                { "Mode", dryRun ? "DRY_RUN" : "NORMAL" },
                { "SourceSubscription", sourceSubscription },
                { "TargetSubscription", targetSubscription }
            };

            _operationLogger.LogOperationEvent(
                operationId,
                "COPY_COMMAND_START",
                "Copy command execution started",
                LogLevel.Information,
                metadata: commandMetadata);

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
            // tenantIdは必須パラメータとしてCLIで検証済み

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

            _logger.LogInformation("ARM clients created for source and target subscriptions");

            // ギャラリーリソースを取得
            var sourceGalleryCollection = _clientFactory.GetGalleryCollection(sourceContext, sourceClient);
            var targetGalleryCollection = _clientFactory.GetGalleryCollection(targetContext, targetClient);

            _logger.LogInformation("Retrieving source gallery '{SourceGallery}'", sourceGallery);
            var sourceGalleryResponse = await sourceGalleryCollection.GetAsync(sourceGallery);
            var sourceGalleryResource = sourceGalleryResponse.Value;

            // レスポンスから HTTP ステータスコードを取得
            var sourceGalleryHttpStatus = sourceGalleryResponse.GetRawResponse()?.Status.ToString() ?? "Unknown";

            var sourceGalleryMetadata = new Dictionary<string, string>
            {
                { "ResourceId", sourceGalleryResource.Id.ToString() },
                { "GalleryName", GetNameFromId(sourceGalleryResource.Id.ToString()) },
                { "Location", sourceGalleryResource.Data.Location.ToString() },
                { "HttpStatus", sourceGalleryHttpStatus },
                { "ErrorCode", "None" }
            };

            _operationLogger.LogOperationEvent(
                operationId,
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Retrieved source gallery: {sourceGallery}",
                LogLevel.Information,
                metadata: sourceGalleryMetadata);

            _logger.LogInformation("Retrieving target gallery '{TargetGallery}'", targetGallery);
            var targetGalleryResponse = await targetGalleryCollection.GetAsync(targetGallery);
            var targetGalleryResource = targetGalleryResponse.Value;

            // レスポンスから HTTP ステータスコードを取得
            var targetGalleryHttpStatus = targetGalleryResponse.GetRawResponse()?.Status.ToString() ?? "Unknown";

            var targetGalleryMetadata = new Dictionary<string, string>
            {
                { "ResourceId", targetGalleryResource.Id.ToString() },
                { "GalleryName", GetNameFromId(targetGalleryResource.Id.ToString()) },
                { "Location", targetGalleryResource.Data.Location.ToString() },
                { "HttpStatus", targetGalleryHttpStatus },
                { "ErrorCode", "None" }
            };

            _operationLogger.LogOperationEvent(
                operationId,
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Retrieved target gallery: {targetGallery}",
                LogLevel.Information,
                metadata: targetGalleryMetadata);

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

            // サマリーを出力（ドライランモードに応じて出力を分岐）
            PrintSummary(summary, dryRun);

            // 終了コードを決定
            Environment.Exit(DetermineExitCode(summary, dryRun));
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError("Azure API request failed: Status={Status}, ErrorCode={ErrorCode}, Message={Message}",
                ex.Status, ex.ErrorCode, ex.Message);

            var errorMetadata = new Dictionary<string, string>
            {
                { "ErrorType", "RequestFailedException" },
                { "HttpStatus", ex.Status.ToString() },
                { "ErrorCode", ex.ErrorCode ?? "Unknown" },
                { "Message", ex.Message }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                "COPY_COMMAND_FAILED",
                $"Copy command failed with Azure API error: {ex.Message}",
                LogLevel.Error,
                ex,
                errorMetadata);

            Environment.Exit(4);
        }
        catch (Exception ex)
        {
            _logger.LogError("Copy command failed: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);

            var errorMetadata = new Dictionary<string, string>
            {
                { "ErrorType", ex.GetType().Name },
                { "Message", ex.Message }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                "COPY_COMMAND_FAILED",
                $"Copy command failed: {ex.Message}",
                LogLevel.Error,
                ex,
                errorMetadata);

            Environment.Exit(4); // 認証エラーまたはその他の予期しないエラー
        }
    }

    /// <summary>
    /// サマリーを出力
    /// </summary>
    private void PrintSummary(CopySummary summary, bool isDryRun)
    {
        if (isDryRun)
        {
            _dryRunPrinter.PrintDryRunPlan(summary);
        }
        else
        {
            _summaryPrinter.PrintSummary(summary);
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
    private int DetermineExitCode(CopySummary summary, bool isDryRun)
    {
        if (isDryRun)
        {
            return _dryRunPrinter.DetermineDryRunExitCode(summary);
        }
        else
        {
            return _summaryPrinter.DetermineExitCode(summary);
        }
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
