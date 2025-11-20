namespace AzureComputeGalleryCopy.Models;

/// <summary>
/// コピー操作全体の結果サマリー
/// </summary>
public class CopySummary
{
    /// <summary>
    /// 操作開始時刻
    /// </summary>
    public DateTimeOffset StartTime { get; set; }
    
    /// <summary>
    /// 操作終了時刻
    /// </summary>
    public DateTimeOffset EndTime { get; set; }
    
    /// <summary>
    /// ソースコンテキスト
    /// </summary>
    public AzureContext SourceContext { get; set; } = new AzureContext
    {
        TenantId = "",
        SubscriptionId = "",
        ResourceGroupName = "",
        GalleryName = ""
    };
    
    /// <summary>
    /// ターゲットコンテキスト
    /// </summary>
    public AzureContext TargetContext { get; set; } = new AzureContext
    {
        TenantId = "",
        SubscriptionId = "",
        ResourceGroupName = "",
        GalleryName = ""
    };
    
    /// <summary>
    /// 全操作のリスト
    /// </summary>
    public IList<CopyOperation> Operations { get; set; } = new List<CopyOperation>();
    
    /// <summary>
    /// 作成されたイメージ定義数
    /// </summary>
    public int CreatedImageDefinitions { get; set; }
    
    /// <summary>
    /// 作成されたイメージバージョン数
    /// </summary>
    public int CreatedImageVersions { get; set; }
    
    /// <summary>
    /// スキップされたイメージバージョン数
    /// </summary>
    public int SkippedImageVersions { get; set; }
    
    /// <summary>
    /// 失敗した操作数
    /// </summary>
    public int FailedOperations { get; set; }
    
    /// <summary>
    /// ドライランモードか
    /// </summary>
    public bool IsDryRun { get; set; }
}
