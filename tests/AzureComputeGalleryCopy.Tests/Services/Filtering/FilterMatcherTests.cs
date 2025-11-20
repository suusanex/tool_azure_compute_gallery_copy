using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Services.Filtering;
using NUnit.Framework;

namespace AzureComputeGalleryCopy.Tests.Services.Filtering;

/// <summary>
/// FilterMatcher のユニットテスト
/// </summary>
[TestFixture]
public class FilterMatcherTests
{
    private IFilterMatcher _filterMatcher = null!;

    [SetUp]
    public void SetUp()
    {
        _filterMatcher = new FilterMatcher();
    }

    #region Image Definition Matching Tests

    [Test]
    public void MatchesImageDefinition_NoFilters_ReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria();

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-2204", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_PrefixInclude_MatchingPatternReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["ubuntu"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-2204", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_PrefixInclude_NonMatchingPatternReturnsFalse()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["ubuntu"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("windows-server-2022", criteria);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesImageDefinition_PrefixInclude_CasInsensitiveMatching()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["UBUNTU"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-2204", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_ContainsInclude_MatchingPatternReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["ubuntu"],
            MatchMode = MatchMode.Contains
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("my-ubuntu-image", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_ContainsInclude_NonMatchingPatternReturnsFalse()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["debian"],
            MatchMode = MatchMode.Contains
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-2204", criteria);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesImageDefinition_PrefixExclude_ExcludedPatternReturnsFalse()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionExcludes = ["windows"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("windows-server-2022", criteria);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesImageDefinition_PrefixExclude_NonExcludedPatternReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionExcludes = ["windows"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-2204", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_IncludeAndExclude_IncludeAndNotExcludedReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["ubuntu", "debian"],
            ImageDefinitionExcludes = ["lts"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-2204", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_IncludeAndExclude_IncludedButExcludedReturnsFalse()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["ubuntu"],
            ImageDefinitionExcludes = ["lts"],
            MatchMode = MatchMode.Contains
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-lts-2204", criteria);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesImageDefinition_MultipleIncludePatterns_OneMatchingReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["ubuntu", "debian"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("debian-11", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_MultipleExcludePatterns_OneMatchingReturnsFalse()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionExcludes = ["lts", "preview"],
            MatchMode = MatchMode.Contains
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-preview-2204", criteria);

        // Assert
        Assert.That(result, Is.False);
    }

    #endregion

    #region Version Matching Tests

    [Test]
    public void MatchesVersion_NoFilters_ReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria();

        // Act
        var result = _filterMatcher.MatchesVersion("1.0.0", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesVersion_PrefixInclude_MatchingPatternReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            VersionIncludes = ["1.0"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesVersion("1.0.0", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesVersion_PrefixInclude_NonMatchingPatternReturnsFalse()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            VersionIncludes = ["2.0"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesVersion("1.0.0", criteria);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesVersion_ContainsInclude_MatchingPatternReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            VersionIncludes = [".0.0"],
            MatchMode = MatchMode.Contains
        };

        // Act
        var result = _filterMatcher.MatchesVersion("1.0.0", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesVersion_PrefixExclude_ExcludedPatternReturnsFalse()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            VersionExcludes = ["2.0"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesVersion("2.0.5", criteria);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void MatchesVersion_IncludeAndExclude_IncludeAndNotExcludedReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            VersionIncludes = ["1"],
            VersionExcludes = ["1.0.0"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesVersion("1.0.1", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesVersion_IncludeAndExclude_IncludedButExcludedReturnsFalse()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            VersionIncludes = ["1"],
            VersionExcludes = ["1.0.0"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesVersion("1.0.0", criteria);

        // Assert
        Assert.That(result, Is.False);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void MatchesImageDefinition_EmptyPatternList_ReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = [],
            ImageDefinitionExcludes = []
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("any-image", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_ExactMatch_Prefix_ReturnsTrue()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["ubuntu-2204"],
            MatchMode = MatchMode.Prefix
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("ubuntu-2204", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void MatchesImageDefinition_SpecialCharactersInPattern_ReturnsCorrectResult()
    {
        // Arrange
        var criteria = new FilterCriteria
        {
            ImageDefinitionIncludes = ["ubuntu-2204"],
            MatchMode = MatchMode.Contains
        };

        // Act
        var result = _filterMatcher.MatchesImageDefinition("my-ubuntu-2204-image", criteria);

        // Assert
        Assert.That(result, Is.True);
    }

    #endregion

    #region Argument Validation

    [Test]
    public void MatchesImageDefinition_NullImageName_ThrowsArgumentException()
    {
        // Arrange
        var criteria = new FilterCriteria();

        // Act & Assert
        Assert.That(
            () => _filterMatcher.MatchesImageDefinition(null!, criteria),
            Throws.InstanceOf<ArgumentException>()
        );
    }

    [Test]
    public void MatchesImageDefinition_EmptyImageName_ThrowsArgumentException()
    {
        // Arrange
        var criteria = new FilterCriteria();

        // Act & Assert
        Assert.That(
            () => _filterMatcher.MatchesImageDefinition(string.Empty, criteria),
            Throws.InstanceOf<ArgumentException>()
        );
    }

    [Test]
    public void MatchesImageDefinition_NullCriteria_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.That(
            () => _filterMatcher.MatchesImageDefinition("ubuntu-2204", null!),
            Throws.InstanceOf<ArgumentNullException>()
        );
    }

    [Test]
    public void MatchesVersion_NullVersionName_ThrowsArgumentException()
    {
        // Arrange
        var criteria = new FilterCriteria();

        // Act & Assert
        Assert.That(
            () => _filterMatcher.MatchesVersion(null!, criteria),
            Throws.InstanceOf<ArgumentException>()
        );
    }

    [Test]
    public void MatchesVersion_EmptyVersionName_ThrowsArgumentException()
    {
        // Arrange
        var criteria = new FilterCriteria();

        // Act & Assert
        Assert.That(
            () => _filterMatcher.MatchesVersion(string.Empty, criteria),
            Throws.InstanceOf<ArgumentException>()
        );
    }

    [Test]
    public void MatchesVersion_NullCriteria_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.That(
            () => _filterMatcher.MatchesVersion("1.0.0", null!),
            Throws.InstanceOf<ArgumentNullException>()
        );
    }

    #endregion
}
