using AzureComputeGalleryCopy.Models;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Cli.Output;

/// <summary>
/// コピー結果のサマリー出力を処理
/// </summary>
public class SummaryPrinter
{
    private readonly ILogger<SummaryPrinter> _logger;

    /// <summary>
    /// SummaryPrinterのコンストラクタ
    /// </summary>
    public SummaryPrinter(ILogger<SummaryPrinter> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// コピー結果サマリーを出力
    /// </summary>
    public void PrintSummary(CopySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        _logger.LogInformation("Printing copy summary");

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
        PrintSkippedOperations(summary);

        // 失敗した操作を表示
        PrintFailedOperations(summary);

        _logger.LogInformation("Summary printed");
    }

    /// <summary>
    /// スキップされた操作を表示
    /// </summary>
    private void PrintSkippedOperations(CopySummary summary)
    {
        var skippedOps = summary.Operations.Where(op => op.Result == OperationResult.Skipped).ToList();
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
    /// 失敗した操作を表示
    /// </summary>
    private void PrintFailedOperations(CopySummary summary)
    {
        var failedOps = summary.Operations.Where(op => op.Result == OperationResult.Failed).ToList();
        if (!failedOps.Any())
        {
            return;
        }

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

    /// <summary>
    /// 終了コードを決定
    /// </summary>
    public int DetermineExitCode(CopySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.FailedOperations > 0)
        {
            _logger.LogWarning("Copy operation completed with failures: {FailedCount}", summary.FailedOperations);
            return 1; // 部分的失敗
        }

        _logger.LogInformation("Copy operation completed successfully");
        return 0; // 成功
    }
}
