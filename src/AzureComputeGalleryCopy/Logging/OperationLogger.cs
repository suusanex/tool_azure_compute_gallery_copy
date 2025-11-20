using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Logging;

/// <summary>
/// 操作イベントをトレースするための構造化ロガー
/// 操作ID、オペレーションコードなどの状態を保持して一貫したログを出力
/// </summary>
public interface IOperationLogger
{
    /// <summary>
    /// 操作イベントをログ出力
    /// </summary>
    void LogOperationEvent(
        string operationId,
        string operationCode,
        string message,
        LogLevel level = LogLevel.Information,
        Exception? exception = null,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// 新しい操作IDを生成
    /// </summary>
    string GenerateOperationId();
}

/// <inheritdoc />
public class OperationLogger : IOperationLogger
{
    private readonly ILogger<OperationLogger> _logger;

    // 操作コードの定義
    public static class OperationCode
    {
        // ギャラリー操作
        public const string QueryGallerySuccess = "QUERY_GALLERY_SUCCESS";
        public const string QueryGalleryFailed = "QUERY_GALLERY_FAILED";
        
        // イメージ定義操作
        public const string CreateImageDefSuccess = "CREATE_IMAGE_DEF_SUCCESS";
        public const string CreateImageDefFailed = "CREATE_IMAGE_DEF_FAILED";
        public const string ImageDefExists = "IMAGE_DEF_EXISTS";
        public const string SkipImageDefImmutableMismatch = "SKIP_IMAGE_DEF_IMMUTABLE_MISMATCH";
        
        // イメージバージョン操作
        public const string CreateVersionSuccess = "CREATE_VERSION_SUCCESS";
        public const string CreateVersionFailed = "CREATE_VERSION_FAILED";
        public const string VersionExists = "VERSION_EXISTS";
        public const string SkipVersionRegionUnavailable = "SKIP_VERSION_REGION_UNAVAILABLE";
        
        // フィルタ操作
        public const string ApplyFilterSuccess = "APPLY_FILTER_SUCCESS";
        public const string ApplyFilterFailed = "APPLY_FILTER_FAILED";
        public const string FilteredOutImage = "FILTERED_OUT_IMAGE";
        public const string FilteredOutVersion = "FILTERED_OUT_VERSION";
        
        // 設定操作
        public const string ConfigLoadSuccess = "CONFIG_LOAD_SUCCESS";
        public const string ConfigLoadFailed = "CONFIG_LOAD_FAILED";
        public const string ConfigValidationSuccess = "CONFIG_VALIDATION_SUCCESS";
        public const string ConfigValidationFailed = "CONFIG_VALIDATION_FAILED";
        
        // 認証操作
        public const string AuthenticationSuccess = "AUTH_SUCCESS";
        public const string AuthenticationFailed = "AUTH_FAILED";
        
        // ドライラン操作
        public const string DryRunExecutionStart = "DRY_RUN_START";
        public const string DryRunExecutionComplete = "DRY_RUN_COMPLETE";
    }

    /// <summary>
    /// OperationLoggerのコンストラクタ
    /// </summary>
    public OperationLogger(ILogger<OperationLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public void LogOperationEvent(
        string operationId,
        string operationCode,
        string message,
        LogLevel level = LogLevel.Information,
        Exception? exception = null,
        Dictionary<string, string>? metadata = null)
    {
        // メタデータをログに追加
        var logMessage = BuildLogMessage(operationId, operationCode, message, metadata);

        // ログレベルに応じてログ出力
        if (exception != null)
        {
            _logger.Log(level, exception, logMessage);
        }
        else
        {
            _logger.Log(level, logMessage);
        }
    }

    /// <inheritdoc />
    public string GenerateOperationId()
    {
        // UUIDベースの操作ID生成
        return $"OP-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// ログメッセージを構築
    /// </summary>
    private static string BuildLogMessage(
        string operationId,
        string operationCode,
        string message,
        Dictionary<string, string>? metadata)
    {
        var logMessage = $"[{operationId}] [{operationCode}] {message}";

        if (metadata != null && metadata.Count > 0)
        {
            var metadataStr = string.Join(", ", metadata.Select(kv => $"{kv.Key}={kv.Value}"));
            logMessage += $" | {metadataStr}";
        }

        return logMessage;
    }
}
