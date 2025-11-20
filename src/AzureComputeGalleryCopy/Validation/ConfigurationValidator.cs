using System.Text.RegularExpressions;
using AzureComputeGalleryCopy.Models;

namespace AzureComputeGalleryCopy.Validation;

/// <summary>
/// 設定のバリデーションを行います。
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// 設定をバリデーションします。
    /// </summary>
    /// <param name="configuration">バリデーション対象の設定</param>
    /// <returns>バリデーションエラーがある場合、エラーメッセージのリスト。バリデーション成功時は空のリスト。</returns>
    Task<IList<string>> ValidateAsync(ToolConfiguration configuration);
}

/// <inheritdoc />
public class ConfigurationValidator : IConfigurationValidator
{
    private const string GuidPattern =
        @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";

    private const string ResourceGroupNamePattern = @"^[a-zA-Z0-9\-_.()]+$";

    private const string GalleryNamePattern = @"^[a-zA-Z0-9.]+$";

    /// <inheritdoc />
    public async Task<IList<string>> ValidateAsync(ToolConfiguration configuration)
    {
        var errors = new List<string>();

        if (configuration == null)
        {
            errors.Add("Configuration is required.");
            return errors;
        }

        // ソースコンテキストの検証
        ValidateAzureContext(configuration.Source, "Source", errors);

        // ターゲットコンテキストの検証
        ValidateAzureContext(configuration.Target, "Target", errors);

        // 認証設定の検証
        ValidateAuthenticationConfiguration(configuration.Authentication, errors);

        // 同一テナント制約の検証
        if (!string.IsNullOrEmpty(configuration.Source?.TenantId) &&
            !string.IsNullOrEmpty(configuration.Target?.TenantId))
        {
            if (configuration.Source.TenantId != configuration.Target.TenantId)
            {
                errors.Add(
                    $"Source and target must be in the same tenant.\n  Source TenantId: {configuration.Source.TenantId}\n  Target TenantId: {configuration.Target.TenantId}\n\n  To fix: Ensure both subscriptions belong to the same Azure AD tenant.");
            }
        }

        // 認証設定のテナントIDが一致するか検証
        if (!string.IsNullOrEmpty(configuration.Source?.TenantId) &&
            !string.IsNullOrEmpty(configuration.Authentication?.TenantId))
        {
            if (configuration.Source.TenantId != configuration.Authentication.TenantId)
            {
                errors.Add(
                    $"Authentication tenant ID must match source and target tenant ID.\n  Source TenantId: {configuration.Source.TenantId}\n  Authentication TenantId: {configuration.Authentication.TenantId}");
            }
        }

        // ログレベルの検証
        var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        if (!string.IsNullOrEmpty(configuration.LogLevel) &&
            !validLogLevels.Contains(configuration.LogLevel))
        {
            errors.Add(
                $"Invalid log level '{configuration.LogLevel}'. Valid levels are: {string.Join(", ", validLogLevels)}");
        }

        // フィルタの検証
        ValidateFilterCriteria(configuration.Filter, errors);

        return await Task.FromResult(errors);
    }

    /// <summary>
    /// AzureContextをバリデーションします。
    /// </summary>
    private static void ValidateAzureContext(AzureContext? context, string contextName,
        IList<string> errors)
    {
        if (context == null)
        {
            errors.Add($"{contextName} context is required.");
            return;
        }

        // TenantId の検証（オプション）
        if (!string.IsNullOrEmpty(context.TenantId))
        {
            if (!Regex.IsMatch(context.TenantId, GuidPattern, RegexOptions.IgnoreCase))
            {
                errors.Add($"{contextName} TenantId must be a valid GUID.");
            }
        }

        // SubscriptionId の検証（必須）
        if (string.IsNullOrEmpty(context.SubscriptionId))
        {
            errors.Add($"{contextName} SubscriptionId is required.");
        }
        else if (!Regex.IsMatch(context.SubscriptionId, GuidPattern, RegexOptions.IgnoreCase))
        {
            errors.Add($"{contextName} SubscriptionId must be a valid GUID.");
        }

        // ResourceGroupName の検証（必須）
        if (string.IsNullOrEmpty(context.ResourceGroupName))
        {
            errors.Add($"{contextName} ResourceGroupName is required.");
        }
        else if (context.ResourceGroupName.Length < 1 || context.ResourceGroupName.Length > 90)
        {
            errors.Add($"{contextName} ResourceGroupName must be 1-90 characters long.");
        }
        else if (!Regex.IsMatch(context.ResourceGroupName, ResourceGroupNamePattern))
        {
            errors.Add(
                $"{contextName} ResourceGroupName contains invalid characters. Valid: a-z, A-Z, 0-9, -, _, ., ( )");
        }

        // GalleryName の検証（必須）
        if (string.IsNullOrEmpty(context.GalleryName))
        {
            errors.Add($"{contextName} GalleryName is required.");
        }
        else if (context.GalleryName.Length < 1 || context.GalleryName.Length > 80)
        {
            errors.Add($"{contextName} GalleryName must be 1-80 characters long.");
        }
        else if (!Regex.IsMatch(context.GalleryName, GalleryNamePattern))
        {
            errors.Add($"{contextName} GalleryName contains invalid characters. Valid: a-z, A-Z, 0-9, .");
        }
    }

    /// <summary>
    /// AuthenticationConfigurationをバリデーションします。
    /// </summary>
    private static void ValidateAuthenticationConfiguration(
        AuthenticationConfiguration? auth, IList<string> errors)
    {
        if (auth == null)
        {
            errors.Add("Authentication configuration is required.");
            return;
        }

        // TenantId の検証（必須）
        if (string.IsNullOrEmpty(auth.TenantId))
        {
            errors.Add("Authentication TenantId is required.");
        }
        else if (!Regex.IsMatch(auth.TenantId, GuidPattern, RegexOptions.IgnoreCase))
        {
            errors.Add("Authentication TenantId must be a valid GUID.");
        }

        // ClientId の検証（必須）
        if (string.IsNullOrEmpty(auth.ClientId))
        {
            errors.Add("Authentication ClientId is required.");
        }
        else if (!Regex.IsMatch(auth.ClientId, GuidPattern, RegexOptions.IgnoreCase))
        {
            errors.Add("Authentication ClientId must be a valid GUID.");
        }
    }

    /// <summary>
    /// FilterCriteriaをバリデーションします。
    /// </summary>
    private static void ValidateFilterCriteria(FilterCriteria? filter, IList<string> errors)
    {
        if (filter == null)
        {
            return; // フィルタはオプション
        }

        // MatchMode の検証
        if (!Enum.IsDefined(typeof(MatchMode), filter.MatchMode))
        {
            errors.Add(
                $"Invalid MatchMode '{filter.MatchMode}'. Valid modes are: {string.Join(", ", Enum.GetNames(typeof(MatchMode)))}");
        }
    }
}
