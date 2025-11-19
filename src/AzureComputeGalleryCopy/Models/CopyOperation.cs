namespace AzureComputeGalleryCopy.Models;

/// <summary>
/// コピー操作の結果
/// </summary>
public enum OperationResult
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,
    
    /// <summary>
    /// スキップ（既存または制約による）
    /// </summary>
    Skipped,
    
    /// <summary>
    /// 失敗
    /// </summary>
    Failed
}

/// <summary>
/// コピー操作のタイプ
/// </summary>
public enum OperationType
{
    /// <summary>
    /// ギャラリーの作成
    /// </summary>
    CreateGallery,
    
    /// <summary>
    /// イメージ定義の作成
    /// </summary>
    CreateImageDefinition,
    
    /// <summary>
    /// イメージバージョンの作成
    /// </summary>
    CreateImageVersion
}

/// <summary>
/// 単一のイメージ定義またはバージョンのコピー操作を表現
/// </summary>
public class CopyOperation
{
    /// <summary>
    /// 操作ID（ログ相関用）
    /// </summary>
    public Guid OperationId { get; set; }
    
    /// <summary>
    /// 操作種別
    /// </summary>
    public OperationType Type { get; set; }
    
    /// <summary>
    /// 対象イメージ定義名
    /// </summary>
    public string ImageDefinitionName { get; set; } = string.Empty;
    
    /// <summary>
    /// 対象バージョン名（バージョンコピーの場合のみ）
    /// </summary>
    public string? VersionName { get; set; }
    
    /// <summary>
    /// 操作結果
    /// </summary>
    public OperationResult Result { get; set; }
    
    /// <summary>
    /// スキップ理由（Result=Skippedの場合）
    /// </summary>
    public string? SkipReason { get; set; }
    
    /// <summary>
    /// エラーメッセージ（Result=Failedの場合）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// エラーコード（Result=Failedの場合、AzureエラーコードまたはHTTPステータス）
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// 操作開始時刻
    /// </summary>
    public DateTimeOffset StartTime { get; set; }
    
    /// <summary>
    /// 操作終了時刻
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }
}
