using Microsoft.Extensions.Configuration;
using AzureComputeGalleryCopy.Models;

namespace AzureComputeGalleryCopy.Tests.TestHelpers;

/// <summary>
/// テスト用の設定オブジェクトを構築するためのヘルパークラス
/// </summary>
public class ConfigurationBuilderHelper
{
    /// <summary>
    /// デフォルトのテスト設定を含むToolConfigurationを構築します。
    /// </summary>
    /// <returns>デフォルト設定を持つToolConfiguration</returns>
    public static ToolConfiguration BuildDefaultConfiguration()
    {
        return new ToolConfiguration
        {
            Source = new AzureContext
            {
                TenantId = "12345678-1234-1234-1234-123456789012",
                SubscriptionId = "11111111-1111-1111-1111-111111111111",
                ResourceGroupName = "source-rg",
                GalleryName = "source-gallery"
            },
            Target = new AzureContext
            {
                TenantId = "12345678-1234-1234-1234-123456789012",
                SubscriptionId = "22222222-2222-2222-2222-222222222222",
                ResourceGroupName = "target-rg",
                GalleryName = "target-gallery"
            },
            Authentication = new AuthenticationConfiguration
            {
                TenantId = "12345678-1234-1234-1234-123456789012",
                ClientId = "abcdef01-abcd-abcd-abcd-abcdef012345"
            },
            Filter = new FilterCriteria(),
            DryRun = false,
            LogLevel = "Information"
        };
    }

    /// <summary>
    /// カスタム設定を持つToolConfigurationを構築します。
    /// </summary>
    /// <param name="configureAction">設定をカスタマイズするアクション</param>
    /// <returns>カスタマイズされたToolConfiguration</returns>
    public static ToolConfiguration BuildConfiguration(
        Action<ToolConfigurationBuilder> configureAction)
    {
        var builder = new ToolConfigurationBuilder();
        configureAction(builder);
        return builder.Build();
    }

    /// <summary>
    /// 設定JSONを含むIConfigurationを構築します。
    /// </summary>
    /// <param name="jsonContent">設定JSON文字列</param>
    /// <returns>IConfiguration</returns>
    public static IConfiguration BuildIConfiguration(string jsonContent)
    {
        var configBuilder = new ConfigurationBuilder();
        
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent)))
        {
            configBuilder.AddJsonStream(stream);
        }

        return configBuilder.Build();
    }
}

/// <summary>
/// ToolConfiguration構築用のビルダークラス
/// </summary>
public class ToolConfigurationBuilder
{
    private AzureContext? _source;
    private AzureContext? _target;
    private AuthenticationConfiguration? _authentication;
    private FilterCriteria _filter = new();
    private bool _dryRun;
    private string _logLevel = "Information";

    /// <summary>
    /// ソースコンテキストを設定します。
    /// </summary>
    public ToolConfigurationBuilder WithSource(AzureContext source)
    {
        _source = source;
        return this;
    }

    /// <summary>
    /// ソースコンテキストをプロパティで設定します。
    /// </summary>
    public ToolConfigurationBuilder WithSource(
        string tenantId,
        string subscriptionId,
        string resourceGroupName,
        string galleryName)
    {
        _source = new AzureContext
        {
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            ResourceGroupName = resourceGroupName,
            GalleryName = galleryName
        };
        return this;
    }

    /// <summary>
    /// ターゲットコンテキストを設定します。
    /// </summary>
    public ToolConfigurationBuilder WithTarget(AzureContext target)
    {
        _target = target;
        return this;
    }

    /// <summary>
    /// ターゲットコンテキストをプロパティで設定します。
    /// </summary>
    public ToolConfigurationBuilder WithTarget(
        string tenantId,
        string subscriptionId,
        string resourceGroupName,
        string galleryName)
    {
        _target = new AzureContext
        {
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            ResourceGroupName = resourceGroupName,
            GalleryName = galleryName
        };
        return this;
    }

    /// <summary>
    /// 認証設定を設定します。
    /// </summary>
    public ToolConfigurationBuilder WithAuthentication(AuthenticationConfiguration authentication)
    {
        _authentication = authentication;
        return this;
    }

    /// <summary>
    /// 認証設定をプロパティで設定します。
    /// </summary>
    public ToolConfigurationBuilder WithAuthentication(string tenantId, string clientId)
    {
        _authentication = new AuthenticationConfiguration
        {
            TenantId = tenantId,
            ClientId = clientId
        };
        return this;
    }

    /// <summary>
    /// フィルタを設定します。
    /// </summary>
    public ToolConfigurationBuilder WithFilter(FilterCriteria filter)
    {
        _filter = filter;
        return this;
    }

    /// <summary>
    /// ドライランモードを設定します。
    /// </summary>
    public ToolConfigurationBuilder WithDryRun(bool dryRun = true)
    {
        _dryRun = dryRun;
        return this;
    }

    /// <summary>
    /// ログレベルを設定します。
    /// </summary>
    public ToolConfigurationBuilder WithLogLevel(string logLevel)
    {
        _logLevel = logLevel;
        return this;
    }

    /// <summary>
    /// ToolConfigurationを構築します。
    /// </summary>
    public ToolConfiguration Build()
    {
        return new ToolConfiguration
        {
            Source = _source ?? throw new InvalidOperationException("Source must be set."),
            Target = _target ?? throw new InvalidOperationException("Target must be set."),
            Authentication = _authentication ??
                             throw new InvalidOperationException("Authentication must be set."),
            Filter = _filter,
            DryRun = _dryRun,
            LogLevel = _logLevel
        };
    }
}
