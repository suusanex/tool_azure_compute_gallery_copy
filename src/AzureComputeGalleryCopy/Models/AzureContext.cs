namespace AzureComputeGalleryCopy.Models;

/// <summary>
/// Azure環境への接続情報を表現します。
/// </summary>
public class AzureContext
{
    /// <summary>
    /// テナントID（同一テナント内のクロスサブスクリプション操作に必須）
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// サブスクリプションID
    /// </summary>
    public required string SubscriptionId { get; init; }

    /// <summary>
    /// リソースグループ名
    /// </summary>
    public required string ResourceGroupName { get; init; }

    /// <summary>
    /// ギャラリー名
    /// </summary>
    public required string GalleryName { get; init; }
}
