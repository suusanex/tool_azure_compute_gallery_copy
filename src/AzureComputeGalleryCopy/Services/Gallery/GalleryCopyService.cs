using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Services.Filtering;
using AzureComputeGalleryCopy.Logging;
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
    private readonly IOperationLogger _operationLogger;

    /// <summary>
    /// GalleryCopyServiceのコンストラクタ
    /// </summary>
    public GalleryCopyService(IGalleryQueryService queryService, IFilterMatcher filterMatcher, 
        ILogger<GalleryCopyService> logger, IOperationLogger operationLogger)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(filterMatcher);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(operationLogger);
        
        _queryService = queryService;
        _filterMatcher = filterMatcher;
        _logger = logger;
        _operationLogger = operationLogger;
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

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", sourceGallery.Id.ToString() },
                { "ImageDefinitionCount", sourceImages.Count.ToString() }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Query gallery: Found {sourceImages.Count} image definition(s)",
                LogLevel.Information,
                metadata: metadata);

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
                var (targetImageDef, targetImageDefExists) = await GetOrCreateImageDefinitionAsync(
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

                    await CopyImageVersionAsync(sourceVersion, targetImageDef, targetImageDefExists, isDryRun, operations);
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
    /// <returns>タプル: (ターゲットイメージ定義リソースまたはソース, ターゲットに既存か)</returns>
    private async Task<(GalleryImageResource? imageResource, bool existsInTarget)> GetOrCreateImageDefinitionAsync(
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
                var targetImageDefResponse = await targetGallery.GetGalleryImages()
                    .GetAsync(imageName);
                var targetImageDef = targetImageDefResponse.Value;

                // 注: 変更不可能な属性の事前検証は実装されていません（制限事項 L-001）
                // Azure API実行時にエラーが発生した場合に検出されます

                _logger.LogInformation("Image definition '{ImageDefinitionName}' already exists in target",
                    imageName);
                
                var metadata = new Dictionary<string, string>
                {
                    { "ResourceId", sourceImageDef.Id.ToString() },
                    { "TargetResourceId", targetImageDef.Id.ToString() },
                    { "ImageName", imageName }
                };
                
                _operationLogger.LogOperationEvent(
                    operationId.ToString(),
                    OperationLogger.OperationCode.ImageDefExists,
                    $"Image definition '{imageName}' already exists in target gallery",
                    LogLevel.Information,
                    metadata: metadata);

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

                return (targetImageDef, true);
            }

            // イメージ定義を作成
            if (!isDryRun)
            {
                _logger.LogInformation("Creating image definition '{ImageDefinitionName}'", imageName);

                // Azure SDK の GalleryImageData は Location が必須コンストラクタパラメータ
                // イメージ定義とギャラリーは同じLocationを使用する（Azureの仕様）
                var imageData = new GalleryImageData(targetGallery.Data.Location);

                // Identifier は必須（コンストラクタで設定）
                var identifier = sourceImageDef.Data.Identifier;
                imageData.Identifier = new GalleryImageIdentifier(
                    identifier.Publisher, 
                    identifier.Offer, 
                    identifier.Sku);

                // 変更不可能な必須属性のコピー（Azure API必須）
                imageData.OSType = sourceImageDef.Data.OSType;
                imageData.OSState = sourceImageDef.Data.OSState;
                
                // アーキテクチャとHyperV世代のコピー（推奨、変更不可能）
                if (sourceImageDef.Data.HyperVGeneration.HasValue)
                {
                    imageData.HyperVGeneration = sourceImageDef.Data.HyperVGeneration;
                }
                if (sourceImageDef.Data.Architecture.HasValue)
                {
                    imageData.Architecture = sourceImageDef.Data.Architecture;
                }

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
                
                // Features のコピー（試行、エラーは警告ログのみ）
                if (sourceImageDef.Data.Features != null && sourceImageDef.Data.Features.Count > 0)
                {
                    try
                    {
                        foreach (var feature in sourceImageDef.Data.Features)
                        {
                            imageData.Features.Add(feature);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to copy Features for image definition '{ImageDefinitionName}': {Message}",
                            imageName, ex.Message);
                    }
                }

                var imageDefCollection = targetGallery.GetGalleryImages();
                var result = await imageDefCollection.CreateOrUpdateAsync(
                    Azure.WaitUntil.Completed, imageName, imageData);

                _logger.LogInformation("Successfully created image definition '{ImageDefinitionName}'",
                    imageName);
                
                // レスポンスから HTTP ステータスコードを取得
                var httpStatus = result.GetRawResponse()?.Status.ToString() ?? "Unknown";
                
                var metadata = new Dictionary<string, string>
                {
                    { "ResourceId", sourceImageDef.Id.ToString() },
                    { "TargetResourceId", result.Value.Id.ToString() },
                    { "ImageName", imageName },
                    { "HttpStatus", httpStatus },
                    { "ErrorCode", "None" }
                };
                
                _operationLogger.LogOperationEvent(
                    operationId.ToString(),
                    OperationLogger.OperationCode.CreateImageDefSuccess,
                    $"Image definition '{imageName}' created successfully",
                    LogLevel.Information,
                    metadata: metadata);

                operations.Add(new CopyOperation
                {
                    OperationId = operationId,
                    Type = OperationType.CreateImageDefinition,
                    ImageDefinitionName = imageName,
                    Result = OperationResult.Success,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                });

                return (result.Value, false);
            }
            else
            {
                // ドライラン時は作成をシミュレート
                _logger.LogInformation("[DRY RUN] Would create image definition '{ImageDefinitionName}'", imageName);
                
                var metadata = new Dictionary<string, string>
                {
                    { "ResourceId", sourceImageDef.Id.ToString() },
                    { "ImageName", imageName },
                    { "Mode", "DRY_RUN" },
                    { "HttpStatus", "200" },
                    { "ErrorCode", "None" }
                };
                
                _operationLogger.LogOperationEvent(
                    operationId.ToString(),
                    OperationLogger.OperationCode.CreateImageDefSuccess,
                    $"[DRY RUN] Image definition '{imageName}' would be created",
                    LogLevel.Information,
                    metadata: metadata);

                operations.Add(new CopyOperation
                {
                    OperationId = operationId,
                    Type = OperationType.CreateImageDefinition,
                    ImageDefinitionName = imageName,
                    Result = OperationResult.Success,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow
                });

                // ドライラン時はソースを返すが、ターゲットには存在しない扱い
                return (sourceImageDef, false);
            }
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError("Failed to create/get image definition '{ImageDefinitionName}': Status={Status}, ErrorCode={ErrorCode}, Message={Message}. Exception: {Exception}",
                imageName, ex.Status, ex.ErrorCode, ex.Message, ex.ToString());

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", sourceImageDef.Id.ToString() },
                { "ImageName", imageName },
                { "HttpStatus", ex.Status.ToString() },
                { "ErrorCode", ex.ErrorCode ?? "Unknown" }
            };
            
            _operationLogger.LogOperationEvent(
                operationId.ToString(),
                OperationLogger.OperationCode.CreateImageDefFailed,
                $"Failed to create image definition '{imageName}': {ex.Message}",
                LogLevel.Error,
                ex,
                metadata);

            operations.Add(new CopyOperation
            {
                OperationId = operationId,
                Type = OperationType.CreateImageDefinition,
                ImageDefinitionName = imageName,
                Result = OperationResult.Failed,
                ErrorMessage = $"Status: {ex.Status}, ErrorCode: {ex.ErrorCode}, Message: {ex.Message}",
                ErrorCode = ex.ErrorCode ?? "CreateImageDefinitionFailed",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            });

            return (null, false);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create/get image definition '{ImageDefinitionName}': {Message}. Exception: {Exception}",
                imageName, ex.Message, ex.ToString());

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", sourceImageDef.Id.ToString() },
                { "ImageName", imageName }
            };
            
            _operationLogger.LogOperationEvent(
                operationId.ToString(),
                OperationLogger.OperationCode.CreateImageDefFailed,
                $"Failed to create image definition '{imageName}': {ex.Message}",
                LogLevel.Error,
                ex,
                metadata);

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

            return (null, false);
        }
    }

    /// <summary>
    /// ソースのイメージバージョンをターゲットにコピー
    /// 既存の場合はスキップ（冪等性）
    /// </summary>
    private async Task CopyImageVersionAsync(
        GalleryImageVersionResource sourceVersion,
        GalleryImageResource targetImageDef,
        bool targetImageDefExists,
        bool isDryRun,
        List<CopyOperation> operations)
    {
        var operationId = Guid.NewGuid();
        var versionName = GetNameFromIdString(sourceVersion.Id.ToString());
        var imageName = GetNameFromIdString(targetImageDef.Id.ToString());

        try
        {
            // ドライラン時で定義が新規作成予定の場合、バージョンも存在しない扱い
            bool versionExists = false;
            if (targetImageDefExists)
            {
                versionExists = await _queryService.ImageVersionExistsAsync(targetImageDef, versionName);
            }

            if (versionExists)
            {
                _logger.LogInformation("Image version '{ImageDefinitionName}/{VersionName}' already exists in target",
                    imageName, versionName);

                var metadata = new Dictionary<string, string>
                {
                    { "ResourceId", sourceVersion.Id.ToString() },
                    { "ImageName", imageName },
                    { "VersionName", versionName }
                };

                _operationLogger.LogOperationEvent(
                    operationId.ToString(),
                    OperationLogger.OperationCode.VersionExists,
                    $"Image version '{imageName}/{versionName}' already exists in target gallery",
                    LogLevel.Information,
                    metadata: metadata);

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

                        var metadata = new Dictionary<string, string>
                        {
                            { "ResourceId", sourceVersion.Id.ToString() },
                            { "ImageName", imageName },
                            { "VersionName", versionName },
                            { "SkipReason", "CMK_ENCRYPTION_DETECTED" }
                        };

                        _operationLogger.LogOperationEvent(
                            operationId.ToString(),
                            OperationLogger.OperationCode.SkipVersionRegionUnavailable,
                            $"Image version '{imageName}/{versionName}' skipped: CMK encryption not supported",
                            LogLevel.Warning,
                            metadata: metadata);

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

                var metadata = new Dictionary<string, string>
                {
                    { "ResourceId", sourceVersion.Id.ToString() },
                    { "ImageName", imageName },
                    { "VersionName", versionName },
                    { "Mode", "DRY_RUN" },
                    { "HttpStatus", "201" },
                    { "ErrorCode", "None" }
                };

                _operationLogger.LogOperationEvent(
                    operationId.ToString(),
                    OperationLogger.OperationCode.CreateVersionSuccess,
                    $"[DRY RUN] Image version '{imageName}/{versionName}' would be created",
                    LogLevel.Information,
                    metadata: metadata);

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

                // StorageProfile.Source のコピー（必須）
                // 注: StorageProfile.Source は write-only プロパティのため、GET 応答では常に null。
                // 複製時には既存ギャラリーイメージバージョンのARM IDを指定する。
                versionData.StorageProfile = new GalleryImageVersionStorageProfile
                {
                    Source = new GalleryArtifactVersionFullSource
                    {
                        Id = sourceVersion.Id
                    }
                };

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

                    // TargetRegions のコピー（必須、仕様FR-009）
                    // 注: 完全なリージョン可用性チェックは未実装（制限事項）。
                    // ターゲットサブスクリプションで利用できないリージョンが含まれている場合、
                    // Azure API実行時にエラーが発生する可能性があります。
                    // ここでは一般的な非公開リージョンを警告レベルでログ出力します。
                    // CMK暗号化されたリージョンは既に事前スキップ済み
                    if (sourceVersion.Data.PublishingProfile.TargetRegions != null && 
                        sourceVersion.Data.PublishingProfile.TargetRegions.Count > 0)
                    {
                        foreach (var targetRegion in sourceVersion.Data.PublishingProfile.TargetRegions)
                        {
                            // 暗号化設定を除外してコピー（制限事項 L-004により既にチェック済み）
                            var newTargetRegion = new TargetRegion(targetRegion.Name)
                            {
                                RegionalReplicaCount = targetRegion.RegionalReplicaCount,
                                StorageAccountType = targetRegion.StorageAccountType
                            };
                            publishingProfile.TargetRegions.Add(newTargetRegion);
                            
                            // 一般的な非公開リージョンの警告（完全チェックではない）
                            var regionName = targetRegion.Name?.ToString()?.ToLowerInvariant();
                            if (!string.IsNullOrEmpty(regionName) && 
                                (regionName.Contains("euap") || regionName.Contains("canary") || 
                                 regionName.Contains("stage") || regionName.Contains("test")))
                            {
                                _logger.LogWarning("Version '{ImageDefinitionName}/{VersionName}' includes potentially unavailable region '{Region}'. " +
                                    "If this region is not available in target subscription, the operation will fail.",
                                    imageName, versionName, regionName);
                            }
                        }
                    }
                    else
                    {
                        // TargetRegionsが未設定の場合、ターゲットギャラリーのLocationをデフォルトとして設定
                        _logger.LogWarning("Source version '{ImageDefinitionName}/{VersionName}' has no TargetRegions, using target gallery location as default",
                            imageName, versionName);
                        publishingProfile.TargetRegions.Add(new TargetRegion(targetImageDef.Data.Location));
                    }
                }
                else
                {
                    // PublishingProfileが未設定の場合、最小構成を設定
                    publishingProfile.ReplicaCount = 1;
                    publishingProfile.ExcludeFromLatest = false;
                    publishingProfile.TargetRegions.Add(new TargetRegion(targetImageDef.Data.Location));
                }

                versionData.PublishingProfile = publishingProfile;

                var versionCollection = targetImageDef.GetGalleryImageVersions();
                var result = await versionCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, versionName, versionData);

                _logger.LogInformation("Successfully created image version '{ImageDefinitionName}/{VersionName}'",
                    imageName, versionName);

                // レスポンスから HTTP ステータスコードを取得
                var httpStatus = result.GetRawResponse()?.Status.ToString() ?? "Unknown";

                var metadata = new Dictionary<string, string>
                {
                    { "ResourceId", sourceVersion.Id.ToString() },
                    { "TargetResourceId", result.Value.Id.ToString() },
                    { "ImageName", imageName },
                    { "VersionName", versionName },
                    { "HttpStatus", httpStatus },
                    { "ErrorCode", "None" }
                };

                _operationLogger.LogOperationEvent(
                    operationId.ToString(),
                    OperationLogger.OperationCode.CreateVersionSuccess,
                    $"Image version '{imageName}/{versionName}' created successfully",
                    LogLevel.Information,
                    metadata: metadata);

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
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError("Failed to copy image version '{ImageDefinitionName}/{VersionName}': Status={Status}, ErrorCode={ErrorCode}, Message={Message}. Exception: {Exception}",
                imageName, versionName, ex.Status, ex.ErrorCode, ex.Message, ex.ToString());

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", sourceVersion.Id.ToString() },
                { "ImageName", imageName },
                { "VersionName", versionName },
                { "HttpStatus", ex.Status.ToString() },
                { "ErrorCode", ex.ErrorCode ?? "Unknown" }
            };

            _operationLogger.LogOperationEvent(
                operationId.ToString(),
                OperationLogger.OperationCode.CreateVersionFailed,
                $"Failed to copy image version '{imageName}/{versionName}': {ex.Message}",
                LogLevel.Error,
                ex,
                metadata);

            operations.Add(new CopyOperation
            {
                OperationId = operationId,
                Type = OperationType.CreateImageVersion,
                ImageDefinitionName = imageName,
                VersionName = versionName,
                Result = OperationResult.Failed,
                ErrorMessage = $"Status: {ex.Status}, ErrorCode: {ex.ErrorCode}, Message: {ex.Message}",
                ErrorCode = ex.ErrorCode ?? "CreateImageVersionFailed",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to copy image version '{ImageDefinitionName}/{VersionName}': {Message}. Exception: {Exception}",
                imageName, versionName, ex.Message, ex.ToString());

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", sourceVersion.Id.ToString() },
                { "ImageName", imageName },
                { "VersionName", versionName }
            };

            _operationLogger.LogOperationEvent(
                operationId.ToString(),
                OperationLogger.OperationCode.CreateVersionFailed,
                $"Failed to copy image version '{imageName}/{versionName}': {ex.Message}",
                LogLevel.Error,
                ex,
                metadata);

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

    /// <summary>
    /// Azure Resource IDから最後のリソース名を抽出
    /// </summary>
    /// <param name="id">Azure Resource ID（例: /subscriptions/{sub}/resourceGroups/{rg}/...）</param>
    /// <returns>リソース名</returns>
    private string GetNameFromIdString(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        try
        {
            // Azure SDK の ResourceIdentifier を使用して安全にパース
            var resourceId = new Azure.Core.ResourceIdentifier(id);
            return resourceId.Name;
        }
        catch (Exception ex)
        {
            // パースに失敗した場合はフォールバック（従来のロジック）
            _logger.LogWarning("Failed to parse resource ID '{ResourceId}' using ResourceIdentifier: {Message}. Falling back to string split.",
                id, ex.Message);
            var parts = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[^1] : string.Empty;
        }
    }
}
