namespace AzureComputeGalleryCopy.Models;

/// <summary>
/// 認証設定
/// </summary>
public class AuthenticationConfiguration
{
    /// <summary>
    /// テナントID（ソースとターゲットで共通）
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// アプリ登録のクライアントID
    /// </summary>
    public required string ClientId { get; init; }
}

/// <summary>
/// ツール全体の設定
/// </summary>
public class ToolConfiguration
{
    /// <summary>
    /// ソースのAzureコンテキスト
    /// </summary>
    public required AzureContext Source { get; init; }

    /// <summary>
    /// ターゲットのAzureコンテキスト
    /// </summary>
    public required AzureContext Target { get; init; }

    /// <summary>
    /// フィルタ基準
    /// </summary>
    public FilterCriteria Filter { get; init; } = new();

    /// <summary>
    /// ドライランモード
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// ログレベル（Trace, Debug, Information, Warning, Error, Critical）
    /// </summary>
    public string LogLevel { get; init; } = "Information";

    /// <summary>
    /// 認証設定
    /// </summary>
    public required AuthenticationConfiguration Authentication { get; init; }
}
