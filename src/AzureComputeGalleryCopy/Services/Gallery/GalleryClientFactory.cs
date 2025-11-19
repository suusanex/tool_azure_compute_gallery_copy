using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using AzureComputeGalleryCopy.Models;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Services.Gallery;

/// <summary>
/// Azure Compute Galleryクライアントファクトリ
/// 認証情報を基にComputeManagementClientのインスタンスを生成
/// </summary>
public class GalleryClientFactory : IGalleryClientFactory
{
    private readonly ILogger<GalleryClientFactory> _logger;
    private readonly TokenCredential _credential;

    /// <summary>
    /// GalleryClientFactoryのコンストラクタ
    /// </summary>
    public GalleryClientFactory(TokenCredential credential, ILogger<GalleryClientFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(logger);
        
        _credential = credential;
        _logger = logger;
    }

    /// <summary>
    /// 指定されたテナント・サブスクリプションのArmClientを生成
    /// </summary>
    /// <param name="tenantId">テナントID</param>
    /// <param name="subscriptionId">サブスクリプションID</param>
    /// <returns>ArmClient インスタンス</returns>
    public ArmClient CreateArmClient(string tenantId, string subscriptionId)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNullOrEmpty(subscriptionId);

        _logger.LogDebug("Creating ArmClient for subscription '{SubscriptionId}' in tenant '{TenantId}'", 
            subscriptionId, tenantId);

        // テナント情報を指定して ArmClient を作成
        var armClient = new ArmClient(_credential, subscriptionId);
        
        _logger.LogDebug("ArmClient created successfully");
        return armClient;
    }

    /// <summary>
    /// 指定されたAzureContextに対応するGalleriesOperationsを取得
    /// </summary>
    /// <param name="context">AzureContext</param>
    /// <returns>GalleryCollection</returns>
    public GalleryCollection GetGalleryCollection(AzureContext context, ArmClient armClient)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(armClient);
        ArgumentNullException.ThrowIfNullOrEmpty(context.ResourceGroupName);

        _logger.LogDebug("Getting GalleryCollection for resource group '{ResourceGroupName}' in subscription '{SubscriptionId}'",
            context.ResourceGroupName, context.SubscriptionId);

        // リソースグループを取得
        var resourceGroupId = new Azure.Core.ResourceIdentifier(
            $"/subscriptions/{context.SubscriptionId}/resourceGroups/{context.ResourceGroupName}");
        var resourceGroup = armClient.GetResourceGroupResource(resourceGroupId);
        
        // ギャラリーコレクションを取得
        var galleryCollection = resourceGroup.GetGalleries();
        
        _logger.LogDebug("GalleryCollection retrieved successfully");
        return galleryCollection;
    }
}
