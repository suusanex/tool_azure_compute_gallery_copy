using AzureComputeGalleryCopy.Models;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Cli.Output;

/// <summary>
/// ドライラン計画出力を処理
/// 実際のコピー実行なしに、予定される操作を表示
/// </summary>
public class DryRunPrinter
{
    private readonly ILogger<DryRunPrinter> _logger;

    /// <summary>
    /// DryRunPrinterのコンストラクタ
    /// </summary>
    public DryRunPrinter(ILogger<DryRunPrinter> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// ドライラン計画を出力
    /// </summary>
    public void PrintDryRunPlan(CopySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (!summary.IsDryRun)
        {
            _logger.LogWarning("Attempted to print dry-run plan for a non-dry-run summary");
            return;
        }

        _logger.LogInformation("Printing dry-run plan");

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("Copy Plan Summary (Dry Run)");
        Console.WriteLine("========================================");
        Console.WriteLine($"Duration: {summary.EndTime - summary.StartTime:hh\\:mm\\:ss}");
        Console.WriteLine($"Source: Subscription '{summary.SourceContext.SubscriptionId}', " +
            $"Gallery '{summary.SourceContext.GalleryName}'");
        Console.WriteLine($"Target: Subscription '{summary.TargetContext.SubscriptionId}', " +
            $"Gallery '{summary.TargetContext.GalleryName}'");
        Console.WriteLine();
        Console.WriteLine("Planned Operations (no changes will be made):");
        Console.WriteLine($"  Image Definitions to Create: {summary.CreatedImageDefinitions}");
        Console.WriteLine($"  Image Versions to Create: {summary.CreatedImageVersions}");
        Console.WriteLine($"  Image Versions to Skip: {summary.SkippedImageVersions}");
        Console.WriteLine($"  Failed Operations: {summary.FailedOperations}");
        Console.WriteLine();

        // 計画された作成操作を表示
        PrintPlannedCreations(summary);

        // スキップされる操作を表示
        PrintSkippedOperations(summary);

        // 失敗する操作を表示
        PrintFailedOperations(summary);

        _logger.LogInformation("Dry-run plan printed");
    }

    /// <summary>
    /// 計画された作成操作を表示
    /// </summary>
    private void PrintPlannedCreations(CopySummary summary)
    {
        var createdOps = summary.Operations
            .Where(op => op.Result == OperationResult.Success)
            .ToList();

        if (!createdOps.Any())
        {
            return;
        }

        Console.WriteLine("Planned Creations:");
        foreach (var op in createdOps)
        {
            if (op.Type == OperationType.CreateImageVersion)
            {
                Console.WriteLine($"  + Version '{op.ImageDefinitionName}/{op.VersionName}'");
            }
            else if (op.Type == OperationType.CreateImageDefinition)
            {
                Console.WriteLine($"  + Image '{op.ImageDefinitionName}'");
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// スキップされる操作を表示
    /// </summary>
    private void PrintSkippedOperations(CopySummary summary)
    {
        var skippedOps = summary.Operations
            .Where(op => op.Result == OperationResult.Skipped)
            .ToList();

        if (!skippedOps.Any())
        {
            return;
        }

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

    /// <summary>
    /// 失敗する操作を表示
    /// </summary>
    private void PrintFailedOperations(CopySummary summary)
    {
        var failedOps = summary.Operations
            .Where(op => op.Result == OperationResult.Failed)
            .ToList();

        if (!failedOps.Any())
        {
            return;
        }

        Console.WriteLine("Failed Operations:");
        foreach (var op in failedOps)
        {
            if (op.Type == OperationType.CreateImageVersion)
            {
                Console.WriteLine($"  ✗ Version '{op.ImageDefinitionName}/{op.VersionName}': " +
                    $"{op.ErrorMessage} (Code: {op.ErrorCode})");
            }
            else if (op.Type == OperationType.CreateImageDefinition)
            {
                Console.WriteLine($"  ✗ Image '{op.ImageDefinitionName}': " +
                    $"{op.ErrorMessage} (Code: {op.ErrorCode})");
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// ドライラン終了コードを決定
    /// </summary>
    public int DetermineDryRunExitCode(CopySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.FailedOperations > 0)
        {
            _logger.LogWarning("Dry-run completed with failures: {FailedCount}", summary.FailedOperations);
            return 1; // 部分的失敗
        }

        _logger.LogInformation("Dry-run completed successfully");
        return 0; // 成功
    }
}
