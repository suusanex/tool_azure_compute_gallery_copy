using Microsoft.Extensions.Configuration;
using AzureComputeGalleryCopy.Models;

namespace AzureComputeGalleryCopy.Configuration;

/// <summary>
/// ファイル・環境変数・CLIオプションから設定を読み込みます。
/// 優先順位: CLIオプション > 環境変数 > appsettings.json
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// 設定を読み込みます。
    /// </summary>
    /// <returns>読み込んだ設定</returns>
    Task<ToolConfiguration> LoadAsync();
}

/// <inheritdoc />
public class ConfigurationLoader : IConfigurationLoader
{
    private readonly IConfiguration _configuration;

    public ConfigurationLoader(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public async Task<ToolConfiguration> LoadAsync()
    {
        // 設定ファイルから設定をバインド
        var source = _configuration.GetSection("source").Get<AzureContext>();
        var target = _configuration.GetSection("target").Get<AzureContext>();
        var auth = _configuration.GetSection("authentication").Get<AuthenticationConfiguration>();
        var filter = _configuration.GetSection("filter").Get<FilterCriteria>() ?? new FilterCriteria();

        var dryRun = bool.TryParse(_configuration["dryRun"], out var dr) ? dr : false;
        var logLevel = _configuration["logLevel"] ?? "Information";

        // 環境変数で上書き
        source = OverrideFromEnvironment(source, "ACG_COPY_SOURCE_");
        target = OverrideFromEnvironment(target, "ACG_COPY_TARGET_");
        auth = OverrideAuthFromEnvironment(auth);

        if (source == null)
        {
            throw new InvalidOperationException("Source configuration is required.");
        }

        if (target == null)
        {
            throw new InvalidOperationException("Target configuration is required.");
        }

        if (auth == null)
        {
            throw new InvalidOperationException("Authentication configuration is required.");
        }

        return await Task.FromResult(new ToolConfiguration
        {
            Source = source,
            Target = target,
            Authentication = auth,
            Filter = filter,
            DryRun = dryRun,
            LogLevel = logLevel
        });
    }

    /// <summary>
    /// 環境変数でAzureContextを上書きします。
    /// </summary>
    private static AzureContext? OverrideFromEnvironment(AzureContext? context, string prefix)
    {
        var tenantId = Environment.GetEnvironmentVariable($"{prefix}TENANT_ID");
        var subscriptionId = Environment.GetEnvironmentVariable($"{prefix}SUBSCRIPTION_ID");
        var resourceGroup = Environment.GetEnvironmentVariable($"{prefix}RESOURCE_GROUP");
        var gallery = Environment.GetEnvironmentVariable($"{prefix}GALLERY");

        // 環境変数がない場合はそのまま返す
        if (string.IsNullOrEmpty(subscriptionId) && string.IsNullOrEmpty(resourceGroup) &&
            string.IsNullOrEmpty(gallery) && string.IsNullOrEmpty(tenantId))
        {
            return context;
        }

        // 新しいコンテキストを構築
        return new AzureContext
        {
            TenantId = tenantId ?? context?.TenantId ?? "",
            SubscriptionId = subscriptionId ?? context?.SubscriptionId ?? "",
            ResourceGroupName = resourceGroup ?? context?.ResourceGroupName ?? "",
            GalleryName = gallery ?? context?.GalleryName ?? ""
        };
    }

    /// <summary>
    /// 環境変数でAuthenticationConfigurationを上書きします。
    /// </summary>
    private static AuthenticationConfiguration? OverrideAuthFromEnvironment(
        AuthenticationConfiguration? auth)
    {
        var tenantId = Environment.GetEnvironmentVariable("ACG_COPY_TENANT_ID");
        var clientId = Environment.GetEnvironmentVariable("ACG_COPY_CLIENT_ID");

        // 環境変数がない場合はそのまま返す
        if (string.IsNullOrEmpty(tenantId) && string.IsNullOrEmpty(clientId))
        {
            return auth;
        }

        // 新しい認証設定を構築
        return new AuthenticationConfiguration
        {
            TenantId = tenantId ?? auth?.TenantId ?? "",
            ClientId = clientId ?? auth?.ClientId ?? ""
        };
    }
}
