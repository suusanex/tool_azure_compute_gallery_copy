using Azure.ResourceManager.Compute;

namespace AzureComputeGalleryCopy.Services.Gallery;

/// <summary>
/// ギャラリーイメージのクエリサービスインターフェース
/// </summary>
public interface IGalleryQueryService
{
    /// <summary>
    /// 指定されたギャラリーのすべてのイメージ定義を列挙
    /// </summary>
    Task<List<GalleryImageResource>> EnumerateImageDefinitionsAsync(GalleryResource galleryResource);

    /// <summary>
    /// 指定されたイメージ定義のすべてのバージョンを列挙
    /// </summary>
    Task<List<GalleryImageVersionResource>> EnumerateImageVersionsAsync(GalleryImageResource imageDefinitionResource);

    /// <summary>
    /// 指定されたギャラリーのすべてのイメージ定義とバージョンを列挙
    /// </summary>
    Task<Dictionary<GalleryImageResource, List<GalleryImageVersionResource>>> EnumerateAllImagesAndVersionsAsync(GalleryResource galleryResource);

    /// <summary>
    /// 指定されたギャラリーのイメージが存在するかチェック
    /// </summary>
    Task<bool> ImageDefinitionExistsAsync(GalleryResource galleryResource, string imageDefinitionName);

    /// <summary>
    /// 指定されたイメージバージョンが存在するかチェック
    /// </summary>
    Task<bool> ImageVersionExistsAsync(GalleryImageResource imageDefinitionResource, string versionName);
}
