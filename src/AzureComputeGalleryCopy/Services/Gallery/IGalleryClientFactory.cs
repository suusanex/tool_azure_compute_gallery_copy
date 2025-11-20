using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using AzureComputeGalleryCopy.Models;

namespace AzureComputeGalleryCopy.Services.Gallery;

/// <summary>
/// Azure Compute Galleryクライアントファクトリインターフェース
/// </summary>
public interface IGalleryClientFactory
{
    /// <summary>
    /// 指定されたテナント・サブスクリプションのArmClientを生成
    /// </summary>
    ArmClient CreateArmClient(string tenantId, string subscriptionId);

    /// <summary>
    /// 指定されたAzureContextに対応するGalleriesOperationsを取得
    /// </summary>
    GalleryCollection GetGalleryCollection(AzureContext context, ArmClient armClient);
}
