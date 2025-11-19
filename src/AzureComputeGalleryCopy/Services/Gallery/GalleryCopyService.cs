using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Services.Filtering;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Services.Gallery;

/// <summary>
/// ギャラリーイメージコピーサービス
/// ソースからターゲットへのイメージ定義とバージョンの冪等コピーを実行
/// </summary>
public class GalleryCopyService : IGalleryCopyService
{
    private readonly ILogger<GalleryCopyService> _logger;
    private readonly IGalleryQueryService _queryService;
    private readonly IFilterMatcher _filterMatcher;

    /// <summary>
    /// GalleryCopyServiceのコンストラクタ
    /// </summary>
    public GalleryCopyService(IGalleryQueryService queryService, IFilterMatcher filterMatcher, 
        ILogger<GalleryCopyService> logger)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(filterMatcher);
        ArgumentNullException.ThrowIfNull(logger);
        
        _queryService = queryService;
        _filterMatcher = filterMatcher;
        _logger = logger;
    }

    /// <summary>
    /// ソースギャラリーからターゲットギャラリーへ全イメージをコピー
    /// </summary>
    /// <param name="sourceGallery">ソースギャラリーリソース</param>
    /// <param name="targetGallery">ターゲットギャラリーリソース</param>
    /// <param name="sourceContext">ソースAzureContext</param>
    /// <param name="targetContext">ターゲットAzureContext</param>
    /// <param name="filterCriteria">フィルタ基準（オプション）</param>
    /// <param name="isDryRun">ドライランモード</param>
    /// <returns>コピーサマリー</returns>
    public async Task<CopySummary> CopyAllImagesAsync(
        GalleryResource sourceGallery,
        GalleryResource targetGallery,
        AzureContext sourceContext,
        AzureContext targetContext,
        FilterCriteria? filterCriteria = null,
        bool isDryRun = false)
    {
        ArgumentNullException.ThrowIfNull(sourceGallery);
        ArgumentNullException.ThrowIfNull(targetGallery);
        ArgumentNullException.ThrowIfNull(sourceContext);
        ArgumentNullException.ThrowIfNull(targetContext);

        _logger.LogInformation("Starting copy operation (DryRun: {IsDryRun})", isDryRun);

        var startTime = DateTimeOffset.UtcNow;
        var operations = new List<CopyOperation>();
        var summary = new CopySummary
        {
            StartTime = startTime,
            SourceContext = sourceContext,
            TargetContext = targetContext,
            IsDryRun = isDryRun,
            Operations = operations
        };

        try
        {
            // ソースギャラリーのすべてのイメージ定義とバージョンを列挙
            var sourceImages = await _queryService.EnumerateAllImagesAndVersionsAsync(sourceGallery);
            _logger.LogInformation("Found {DefinitionCount} image definition(s) to copy",
                sourceImages.Count);

            // 各イメージ定義をコピー
            foreach (var (sourceImageDef, sourceVersions) in sourceImages)
            {
                var imageName = GetNameFromIdString(sourceImageDef.Id.ToString());

                // フィルタ判定（デフォルトはフィルタなし）
                var filterCriteriaToUse = filterCriteria ?? new FilterCriteria();
                if (!_filterMatcher.MatchesImageDefinition(imageName, filterCriteriaToUse))
                {
                    _logger.LogInformation("Skipping image definition '{ImageDefinitionName}' (filtered out)",
                        imageName);
                    continue;
                }

                // ターゲットのイメージ定義を作成または取得
                var targetImageDef = await GetOrCreateImageDefinitionAsync(
                    sourceImageDef, targetGallery, isDryRun, operations);

                if (targetImageDef == null)
                {
                    continue; // 不整合エラーなどでスキップ
                }

                // 各バージョンをコピー
                foreach (var sourceVersion in sourceVersions)
                {
                    var versionName = GetNameFromIdString(sourceVersion.Id.ToString());

                    // フィルタ判定
                    if (!_filterMatcher.MatchesVersion(versionName, filterCriteriaToUse))
                    {
                        _logger.LogInformation("Skipping version '{VersionName}' (filtered out)",
                            versionName);
                        continue;
                    }

                    await CopyImageVersionAsync(sourceVersion, targetImageDef, isDryRun, operations);
                }
            }

            summary.EndTime = DateTimeOffset.UtcNow;

            // サマリー統計を集計
            UpdateSummaryStats(summary);

            _logger.LogInformation("Copy operation completed. Created: {CreatedDefs} definitions, " +
                "{CreatedVersions} versions. Skipped: {SkippedVersions}. Failed: {FailedOps}",
                summary.CreatedImageDefinitions, summary.CreatedImageVersions,
                summary.SkippedImageVersions, summary.FailedOperations);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError("Copy operation failed with exception: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// ソースのイメージ定義をターゲットに作成またはチェック
    /// 不整合がある場合はスキップ
    /// </summary>
    private async Task<GalleryImageResource?> GetOrCreateImageDefinitionAsync(
        GalleryImageResource sourceImageDef,
        GalleryResource targetGallery,
        bool isDryRun,
        List<CopyOperation> operations)
    {
        var operationId = Guid.NewGuid();
        var imageName = GetNameFromIdString(sourceImageDef.Id.ToString());

        try
        {
            var imageExists = await _queryService.ImageDefinitionExistsAsync(targetGallery, imageName);

            if (imageExists)
            {
                // ターゲットに同じ名前のイメージ定義が存在
                var targetImageDef = await targetGallery.GetGalleryImages()
                    .GetAsync(imageName);

                // 注: 変更不可能な属性の事前検証は実装されていません（制限事項 L-001）
                // Azure API実行時にエラーが発生した場合に検出されます

                _logger.LogInformation("Image definition '{ImageDefinitionName}' already exists in target",
                    imageName);
                operations.Add(new CopyOperation
                {
                    OperationId = operationId,
                    Type = OperationType.CreateImageDefinition,
                    ImageDefinitionName = imageName,
                    Result = OperationResult.Skipped,
                    SkipReason = "Image definition already exists in target gallery",
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                });

                return targetImageDef;
            }

            // イメージ定義を作成
            if (!isDryRun)
            {
                _logger.LogInformation("Creating image definition '{ImageDefinitionName}'", imageName);

                // Azure SDK の GalleryImageData は Location が必須コンストラクタパラメータ
                var imageData = new GalleryImageData(targetGallery.Data.Location);

                // Identifier は必須（コンストラクタで設定）
                var identifier = sourceImageDef.Data.Identifier;
                imageData.Identifier = new GalleryImageIdentifier(
                    identifier.Publisher, 
                    identifier.Offer, 
                    identifier.Sku);

                // 基本属性のコピー（可能な範囲で）
                if (sourceImageDef.Data.Description != null)
                {
                    imageData.Description = sourceImageDef.Data.Description;
                }
                if (sourceImageDef.Data.Eula != null)
                {
                    imageData.Eula = sourceImageDef.Data.Eula;
                }
                if (sourceImageDef.Data.PrivacyStatementUri != null)
                {
                    imageData.PrivacyStatementUri = sourceImageDef.Data.PrivacyStatementUri;
                }
                if (sourceImageDef.Data.ReleaseNoteUri != null)
                {
                    imageData.ReleaseNoteUri = sourceImageDef.Data.ReleaseNoteUri;
                }
                
                // Features のコピー（試行）
                if (sourceImageDef.Data.Features != null)
                {
                    foreach (var feature in sourceImageDef.Data.Features)
                    {
                        imageData.Features.Add(feature);
                    }
                }

                var imageDefCollection = targetGallery.GetGalleryImages();
                var result = await imageDefCollection.CreateOrUpdateAsync(
                    Azure.WaitUntil.Completed, imageName, imageData);

                operations.Add(new CopyOperation
                {
                    OperationId = operationId,
                    Type = OperationType.CreateImageDefinition,
                    ImageDefinitionName = imageName,
                    Result = OperationResult.Success,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                });

                return result.Value;
            }
            else
            {
                // ドライラン時は作成をシミュレート
                _logger.LogInformation("[DRY RUN] Would create image definition '{ImageDefinitionName}'", imageName);
                operations.Add(new CopyOperation
                {
                    OperationId = operationId,
                    Type = OperationType.CreateImageDefinition,
                    ImageDefinitionName = imageName,
                    Result = OperationResult.Success,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                });

                return sourceImageDef; // ドライラン時はソースを返す
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create/get image definition '{ImageDefinitionName}': {Message}",
                imageName, ex.Message);

            operations.Add(new CopyOperation
            {
                OperationId = operationId,
                Type = OperationType.CreateImageDefinition,
                ImageDefinitionName = imageName,
                Result = OperationResult.Failed,
                ErrorMessage = ex.Message,
                ErrorCode = "CreateImageDefinitionFailed",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            });

            return null;
        }
    }

    /// <summary>
    /// ソースのイメージバージョンをターゲットにコピー
    /// 既存の場合はスキップ（冪等性）
    /// </summary>
    private async Task CopyImageVersionAsync(
        GalleryImageVersionResource sourceVersion,
        GalleryImageResource targetImageDef,
        bool isDryRun,
        List<CopyOperation> operations)
    {
        var operationId = Guid.NewGuid();
        var versionName = GetNameFromIdString(sourceVersion.Id.ToString());
        var imageName = GetNameFromIdString(targetImageDef.Id.ToString());

        try
        {
            var versionExists = await _queryService.ImageVersionExistsAsync(targetImageDef, versionName);

            if (versionExists)
            {
                _logger.LogInformation("Image version '{ImageDefinitionName}/{VersionName}' already exists in target",
                    imageName, versionName);

                operations.Add(new CopyOperation
                {
                    OperationId = operationId,
                    Type = OperationType.CreateImageVersion,
                    ImageDefinitionName = imageName,
                    VersionName = versionName,
                    Result = OperationResult.Skipped,
                    SkipReason = "Image version already exists in target gallery",
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                });

                return;
            }

            // CMK暗号化の検出（制限事項 L-004）
            if (sourceVersion.Data.PublishingProfile?.TargetRegions != null)
            {
                foreach (var region in sourceVersion.Data.PublishingProfile.TargetRegions)
                {
                    if (region.Encryption != null)
                    {
                        _logger.LogWarning("Skipping version '{ImageDefinitionName}/{VersionName}': " +
                            "Customer-managed key (CMK) encryption detected. CMK encryption is not supported " +
                            "for cross-subscription copy (Limitation L-004).",
                            imageName, versionName);

                        operations.Add(new CopyOperation
                        {
                            OperationId = operationId,
                            Type = OperationType.CreateImageVersion,
                            ImageDefinitionName = imageName,
                            VersionName = versionName,
                            Result = OperationResult.Skipped,
                            SkipReason = "CMK encryption detected - not supported for cross-subscription copy",
                            StartTime = DateTimeOffset.UtcNow,
                            EndTime = DateTimeOffset.UtcNow
                        });

                        return;
                    }
                }
            }

            if (isDryRun)
            {
                // ドライラン時は作成をシミュレート
                _logger.LogInformation("[DRY RUN] Would create image version '{ImageDefinitionName}/{VersionName}'",
                    imageName, versionName);

                operations.Add(new CopyOperation
                {
                    OperationId = operationId,
                    Type = OperationType.CreateImageVersion,
                    ImageDefinitionName = imageName,
                    VersionName = versionName,
                    Result = OperationResult.Success,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                });
            }
            else
            {
                // 実際のバージョン作成
                _logger.LogInformation("Creating image version '{ImageDefinitionName}/{VersionName}'",
                    imageName, versionName);

                // Azure SDK の GalleryImageVersionData は Location が必須コンストラクタパラメータ
                var versionData = new GalleryImageVersionData(targetImageDef.Data.Location);

                // 公開プロファイルの作成（最小構成、制限事項 L-002, L-003）
                var publishingProfile = new GalleryImageVersionPublishingProfile();
                if (sourceVersion.Data.PublishingProfile != null)
                {
                    publishingProfile.ReplicaCount = sourceVersion.Data.PublishingProfile.ReplicaCount ?? 1;
                    publishingProfile.ExcludeFromLatest = sourceVersion.Data.PublishingProfile.ExcludeFromLatest ?? false;
                    
                    // EndOfLifeDate のコピー（可能な範囲で）
                    if (sourceVersion.Data.PublishingProfile.EndOfLifeOn.HasValue)
                    {
                        publishingProfile.EndOfLifeOn = sourceVersion.Data.PublishingProfile.EndOfLifeOn;
                    }
                }
                
                versionData.PublishingProfile = publishingProfile;

                var versionCollection = targetImageDef.GetGalleryImageVersions();
                await versionCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, versionName, versionData);

                _logger.LogInformation("Successfully created image version '{ImageDefinitionName}/{VersionName}'",
                    imageName, versionName);

                operations.Add(new CopyOperation
                {
                    OperationId = operationId,
                    Type = OperationType.CreateImageVersion,
                    ImageDefinitionName = imageName,
                    VersionName = versionName,
                    Result = OperationResult.Success,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to copy image version '{ImageDefinitionName}/{VersionName}': {Message}",
                imageName, versionName, ex.Message);

            operations.Add(new CopyOperation
            {
                OperationId = operationId,
                Type = OperationType.CreateImageVersion,
                ImageDefinitionName = imageName,
                VersionName = versionName,
                Result = OperationResult.Failed,
                ErrorMessage = ex.Message,
                ErrorCode = "CreateImageVersionFailed",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            });
        }
    }


    /// <summary>
    /// サマリー統計を更新
    /// </summary>
    private void UpdateSummaryStats(CopySummary summary)
    {
        summary.CreatedImageDefinitions = summary.Operations
            .Count(op => op.Type == OperationType.CreateImageDefinition && op.Result == OperationResult.Success);

        summary.CreatedImageVersions = summary.Operations
            .Count(op => op.Type == OperationType.CreateImageVersion && op.Result == OperationResult.Success);

        summary.SkippedImageVersions = summary.Operations
            .Count(op => op.Type == OperationType.CreateImageVersion && op.Result == OperationResult.Skipped);

        summary.FailedOperations = summary.Operations
            .Count(op => op.Result == OperationResult.Failed);
    }

    private static string GetNameFromIdString(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        var parts = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : string.Empty;
    }
}
