namespace AzureComputeGalleryCopy.Models;

/// <summary>
/// パターンマッチ方式（Prefix: 前方一致, Contains: 部分一致）
/// </summary>
public enum MatchMode
{
    /// <summary>
    /// 前方一致（例: "ubuntu" → "ubuntu-20.04", "ubuntu-22.04"）
    /// </summary>
    Prefix,

    /// <summary>
    /// 部分一致（例: "ubuntu" → "my-ubuntu-image", "ubuntu-server"）
    /// </summary>
    Contains
}

/// <summary>
/// コピー対象のイメージ定義・バージョンを絞り込む条件
/// </summary>
public class FilterCriteria
{
    /// <summary>
    /// イメージ定義名のIncludeパターン（前方一致または部分一致）
    /// </summary>
    public List<string> ImageDefinitionIncludes { get; init; } = [];

    /// <summary>
    /// イメージ定義名のExcludeパターン（前方一致または部分一致）
    /// </summary>
    public List<string> ImageDefinitionExcludes { get; init; } = [];

    /// <summary>
    /// バージョン名のIncludeパターン（前方一致または部分一致）
    /// </summary>
    public List<string> VersionIncludes { get; init; } = [];

    /// <summary>
    /// バージョン名のExcludeパターン（前方一致または部分一致）
    /// </summary>
    public List<string> VersionExcludes { get; init; } = [];

    /// <summary>
    /// パターンマッチ方式（Prefix: 前方一致, Contains: 部分一致）
    /// </summary>
    public MatchMode MatchMode { get; init; } = MatchMode.Prefix;
}
