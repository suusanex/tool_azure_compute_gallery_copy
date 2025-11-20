using Azure.ResourceManager.Compute;
using AzureComputeGalleryCopy.Models;

namespace AzureComputeGalleryCopy.Services.Gallery;

/// <summary>
/// ギャラリーイメージコピーサービスインターフェース
/// </summary>
public interface IGalleryCopyService
{
    /// <summary>
    /// ソースギャラリーからターゲットギャラリーへ全イメージをコピー
    /// </summary>
    Task<CopySummary> CopyAllImagesAsync(
        GalleryResource sourceGallery,
        GalleryResource targetGallery,
        AzureContext sourceContext,
        AzureContext targetContext,
        FilterCriteria? filterCriteria = null,
        bool isDryRun = false);
}
