using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Logging;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Services.Gallery;

/// <summary>
/// ギャラリーイメージのクエリサービス
/// ソースギャラリーのイメージ定義とバージョンを列挙
/// </summary>
public class GalleryQueryService : IGalleryQueryService
{
    private readonly ILogger<GalleryQueryService> _logger;
    private readonly IOperationLogger _operationLogger;

    /// <summary>
    /// GalleryQueryServiceのコンストラクタ
    /// </summary>
    public GalleryQueryService(ILogger<GalleryQueryService> logger, IOperationLogger operationLogger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(operationLogger);
        _logger = logger;
        _operationLogger = operationLogger;
    }

    /// <summary>
    /// 指定されたギャラリーのすべてのイメージ定義を列挙
    /// </summary>
    /// <param name="galleryResource">ギャラリーリソース</param>
    /// <returns>イメージ定義のリスト</returns>
    public async Task<List<GalleryImageResource>> EnumerateImageDefinitionsAsync(
        GalleryResource galleryResource)
    {
        ArgumentNullException.ThrowIfNull(galleryResource);

        _logger.LogInformation("Enumerating image definitions from gallery '{GalleryName}'",
            galleryResource.Id.ToString());

        var imageDefinitions = new List<GalleryImageResource>();

        try
        {
            var imageDefinitionCollection = galleryResource.GetGalleryImages();
            
            await foreach (var imageDef in imageDefinitionCollection.GetAllAsync())
            {
                imageDefinitions.Add(imageDef);
                _logger.LogDebug("Found image definition: {ImageDefinitionName}", imageDef.Id.ToString());
            }

            _logger.LogInformation("Found {Count} image definition(s)", imageDefinitions.Count);

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", galleryResource.Id.ToString() },
                { "ImageDefinitionCount", imageDefinitions.Count.ToString() }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Query image definitions: Found {imageDefinitions.Count} definition(s)",
                LogLevel.Information,
                metadata: metadata);

            return imageDefinitions;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to enumerate image definitions: {Message}", ex.Message);

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", galleryResource.Id.ToString() }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGalleryFailed,
                $"Failed to enumerate image definitions: {ex.Message}",
                LogLevel.Error,
                ex,
                metadata);

            throw;
        }
    }

    /// <summary>
    /// 指定されたイメージ定義のすべてのバージョンを列挙
    /// </summary>
    /// <param name="imageDefinitionResource">イメージ定義リソース</param>
    /// <returns>イメージバージョンのリスト</returns>
    public async Task<List<GalleryImageVersionResource>> EnumerateImageVersionsAsync(
        GalleryImageResource imageDefinitionResource)
    {
        ArgumentNullException.ThrowIfNull(imageDefinitionResource);

        _logger.LogInformation("Enumerating image versions for image definition '{ImageDefinitionName}'",
            imageDefinitionResource.Id.ToString());

        var imageVersions = new List<GalleryImageVersionResource>();

        try
        {
            var imageVersionCollection = imageDefinitionResource.GetGalleryImageVersions();
            
            await foreach (var imageVersion in imageVersionCollection.GetAllAsync())
            {
                imageVersions.Add(imageVersion);
                _logger.LogDebug("Found image version: {VersionName}", imageVersion.Id.ToString());
            }

            _logger.LogInformation("Found {Count} image version(s)", imageVersions.Count);

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", imageDefinitionResource.Id.ToString() },
                { "VersionCount", imageVersions.Count.ToString() }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Query image versions: Found {imageVersions.Count} version(s)",
                LogLevel.Information,
                metadata: metadata);

            return imageVersions;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to enumerate image versions: {Message}", ex.Message);

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", imageDefinitionResource.Id.ToString() }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGalleryFailed,
                $"Failed to enumerate image versions: {ex.Message}",
                LogLevel.Error,
                ex,
                metadata);

            throw;
        }
    }

    /// <summary>
    /// 指定されたギャラリーのすべてのイメージ定義とバージョンを列挙
    /// </summary>
    /// <param name="galleryResource">ギャラリーリソース</param>
    /// <returns>イメージ定義とバージョンのマップ</returns>
    public async Task<Dictionary<GalleryImageResource, List<GalleryImageVersionResource>>>
        EnumerateAllImagesAndVersionsAsync(GalleryResource galleryResource)
    {
        ArgumentNullException.ThrowIfNull(galleryResource);

        _logger.LogInformation("Enumerating all image definitions and versions from gallery '{GalleryName}'",
            galleryResource.Id.ToString());

        var result = new Dictionary<GalleryImageResource, List<GalleryImageVersionResource>>();

        try
        {
            var imageDefinitions = await EnumerateImageDefinitionsAsync(galleryResource);

            foreach (var imageDef in imageDefinitions)
            {
                var versions = await EnumerateImageVersionsAsync(imageDef);
                result[imageDef] = versions;
                _logger.LogDebug("Image definition '{ImageDefinitionName}' has {VersionCount} version(s)",
                    imageDef.Id.ToString(), versions.Count);
            }

            var totalVersions = result.Values.Sum(v => v.Count);
            _logger.LogInformation("Total: {DefinitionCount} image definition(s), {VersionCount} version(s)",
                result.Count, totalVersions);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to enumerate all images and versions: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 指定されたギャラリーのイメージが存在するかチェック
    /// </summary>
    /// <param name="galleryResource">ギャラリーリソース</param>
    /// <param name="imageDefinitionName">イメージ定義名</param>
    /// <returns>存在する場合はTrue</returns>
    public async Task<bool> ImageDefinitionExistsAsync(GalleryResource galleryResource, 
        string imageDefinitionName)
    {
        ArgumentNullException.ThrowIfNull(galleryResource);
        ArgumentNullException.ThrowIfNullOrEmpty(imageDefinitionName);

        _logger.LogDebug("Checking if image definition '{ImageDefinitionName}' exists", imageDefinitionName);

        try
        {
            var imageDefinitionCollection = galleryResource.GetGalleryImages();
            var exists = await imageDefinitionCollection.ExistsAsync(imageDefinitionName);
            _logger.LogDebug("Image definition '{ImageDefinitionName}' exists: {Exists}", 
                imageDefinitionName, exists);

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", galleryResource.Id.ToString() },
                { "ImageDefinitionName", imageDefinitionName },
                { "Exists", exists.ToString() }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Check image definition existence: '{imageDefinitionName}' = {exists}",
                LogLevel.Debug,
                metadata: metadata);

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to check if image definition exists: {Message}", ex.Message);

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", galleryResource.Id.ToString() },
                { "ImageDefinitionName", imageDefinitionName }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGalleryFailed,
                $"Failed to check image definition existence: {ex.Message}",
                LogLevel.Error,
                ex,
                metadata);

            throw;
        }
    }

    /// <summary>
    /// 指定されたイメージバージョンが存在するかチェック
    /// </summary>
    /// <param name="imageDefinitionResource">イメージ定義リソース</param>
    /// <param name="versionName">バージョン名</param>
    /// <returns>存在する場合はTrue</returns>
    public async Task<bool> ImageVersionExistsAsync(GalleryImageResource imageDefinitionResource,
        string versionName)
    {
        ArgumentNullException.ThrowIfNull(imageDefinitionResource);
        ArgumentNullException.ThrowIfNullOrEmpty(versionName);

        _logger.LogDebug("Checking if image version '{VersionName}' exists", versionName);

        try
        {
            var imageVersionCollection = imageDefinitionResource.GetGalleryImageVersions();
            var exists = await imageVersionCollection.ExistsAsync(versionName);
            _logger.LogDebug("Image version '{VersionName}' exists: {Exists}", versionName, exists);

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", imageDefinitionResource.Id.ToString() },
                { "VersionName", versionName },
                { "Exists", exists.ToString() }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Check image version existence: '{versionName}' = {exists}",
                LogLevel.Debug,
                metadata: metadata);

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to check if image version exists: {Message}", ex.Message);

            var metadata = new Dictionary<string, string>
            {
                { "ResourceId", imageDefinitionResource.Id.ToString() },
                { "VersionName", versionName }
            };

            _operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGalleryFailed,
                $"Failed to check image version existence: {ex.Message}",
                LogLevel.Error,
                ex,
                metadata);

            throw;
        }
    }
}
