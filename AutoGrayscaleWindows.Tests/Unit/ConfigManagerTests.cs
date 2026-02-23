using AutoGrayscaleWindows.Models;
using AutoGrayscaleWindows.Services;
using FluentAssertions;
using Xunit;
using MatchType = AutoGrayscaleWindows.Models.MatchType;

namespace AutoGrayscaleWindows.Tests.Unit;

/// <summary>
/// Unit-тесты для ConfigManager
/// </summary>
public class ConfigManagerTests : IDisposable
{
    private readonly ConfigManager _configManager;
    private readonly string _testConfigPath;

    public ConfigManagerTests()
    {
        // Используем временную директорию для тестов
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"AutoGrayscaleTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testConfigPath);
        
        // Устанавливаем путь к конфигурации через переменную окружения
        Environment.SetEnvironmentVariable("APPDATA", _testConfigPath);
        
        _configManager = new ConfigManager();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testConfigPath))
            {
                Directory.Delete(_testConfigPath, true);
            }
        }
        catch
        {
            // Игнорируем ошибки очистки
        }
    }

    [Fact]
    public void Load_WhenNoConfigExists_CreatesDefaultConfig()
    {
        // Arrange & Act
        var config = _configManager.Load();

        // Assert
        config.Should().NotBeNull();
        config.IsEnabled.Should().BeTrue();
        config.Rules.Should().NotBeNull();
        config.Version.Should().Be("1.0");
    }

    [Fact]
    public void Save_And_Load_PreservesData()
    {
        // Arrange
        _configManager.Load();
        _configManager.Config.IsEnabled = false;
        _configManager.Config.AutoStart = true;
        _configManager.Config.ShowNotifications = false;
        _configManager.Config.MinimizeToTray = false;
        _configManager.Config.ReactionDelayMs = 500;
        _configManager.Config.DefaultAction = RuleAction.EnableGrayscale;
        _configManager.Config.Rules.Add(new AppRule
        {
            Id = Guid.NewGuid(),
            AppIdentifier = "chrome.exe",
            DisplayName = "Chrome",
            Action = RuleAction.EnableGrayscale,
            IsActive = true,
            Priority = 10,
            MatchType = MatchType.Contains
        });

        // Act
        _configManager.Save();
        var loadedConfig = _configManager.Load();

        // Assert
        loadedConfig.IsEnabled.Should().BeFalse();
        loadedConfig.AutoStart.Should().BeTrue();
        loadedConfig.ShowNotifications.Should().BeFalse();
        loadedConfig.MinimizeToTray.Should().BeFalse();
        loadedConfig.ReactionDelayMs.Should().Be(500);
        loadedConfig.DefaultAction.Should().Be(RuleAction.EnableGrayscale);
        loadedConfig.Rules.Should().HaveCount(1);
        loadedConfig.Rules[0].AppIdentifier.Should().Be("chrome.exe");
    }

    [Fact]
    public void Validate_WithInvalidReactionDelay_CorrectsValue()
    {
        // Arrange
        var config = new AppConfig
        {
            ReactionDelayMs = -100  // Invalid negative value
        };

        // Act
        var isValid = config.Validate();

        // Assert
        isValid.Should().BeFalse();
        config.ReactionDelayMs.Should().Be(100);  // Reset to default
    }

    [Fact]
    public void Validate_WithTooHighReactionDelay_CorrectsValue()
    {
        // Arrange
        var config = new AppConfig
        {
            ReactionDelayMs = 10000  // Too high
        };

        // Act
        config.Validate();

        // Assert
        config.ReactionDelayMs.Should().Be(100);
    }

    [Fact]
    public void AddRule_AddsRuleToConfig()
    {
        // Arrange
        var config = new AppConfig();
        var rule = new AppRule
        {
            Id = Guid.NewGuid(),
            AppIdentifier = "test.exe",
            DisplayName = "Test App"
        };

        // Act
        config.AddRule(rule);

        // Assert
        config.Rules.Should().Contain(rule);
        config.Rules.Should().HaveCount(1);
    }

    [Fact]
    public void AddRule_WithDuplicateId_ThrowsException()
    {
        // Arrange
        var config = new AppConfig();
        var ruleId = Guid.NewGuid();
        var rule1 = new AppRule { Id = ruleId, AppIdentifier = "test1.exe" };
        var rule2 = new AppRule { Id = ruleId, AppIdentifier = "test2.exe" };
        
        config.AddRule(rule1);

        // Act
        var action = () => config.AddRule(rule2);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveRule_RemovesExistingRule()
    {
        // Arrange
        var config = new AppConfig();
        var ruleId = Guid.NewGuid();
        var rule = new AppRule { Id = ruleId, AppIdentifier = "test.exe" };
        config.AddRule(rule);

        // Act
        var result = config.RemoveRule(ruleId);

        // Assert
        result.Should().BeTrue();
        config.Rules.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        var config = new AppConfig();

        // Act
        var result = config.RemoveRule(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FindRule_ReturnsCorrectRule()
    {
        // Arrange
        var config = new AppConfig();
        var ruleId = Guid.NewGuid();
        var rule = new AppRule { Id = ruleId, AppIdentifier = "test.exe", DisplayName = "Test" };
        config.AddRule(rule);

        // Act
        var found = config.FindRule(ruleId);

        // Assert
        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("Test");
    }

    [Fact]
    public void GetActiveRules_ReturnsOnlyActiveRules()
    {
        // Arrange
        var config = new AppConfig();
        config.Rules = new List<AppRule>
        {
            new() { Id = Guid.NewGuid(), AppIdentifier = "active.exe", IsActive = true, Priority = 10 },
            new() { Id = Guid.NewGuid(), AppIdentifier = "inactive.exe", IsActive = false, Priority = 20 },
            new() { Id = Guid.NewGuid(), AppIdentifier = "active2.exe", IsActive = true, Priority = 5 }
        };

        // Act
        var activeRules = config.GetActiveRules().ToList();

        // Assert
        activeRules.Should().HaveCount(2);
        activeRules[0].AppIdentifier.Should().Be("active.exe");  // Higher priority first
        activeRules[1].AppIdentifier.Should().Be("active2.exe");
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        // Arrange
        var original = new AppConfig
        {
            IsEnabled = false,
            ReactionDelayMs = 500,
            Rules = new List<AppRule>
            {
                new() { Id = Guid.NewGuid(), AppIdentifier = "test.exe" }
            }
        };

        // Act
        var clone = original.Clone();
        
        // Modify original
        original.IsEnabled = true;
        original.ReactionDelayMs = 1000;
        original.Rules.Clear();

        // Assert
        clone.IsEnabled.Should().BeFalse();
        clone.ReactionDelayMs.Should().Be(500);
        clone.Rules.Should().HaveCount(1);
    }

    [Fact]
    public void CreateDefault_ReturnsValidConfig()
    {
        // Act
        var config = AppConfig.CreateDefault();

        // Assert
        config.Should().NotBeNull();
        config.IsEnabled.Should().BeTrue();
        config.AutoStart.Should().BeTrue();
        config.ShowNotifications.Should().BeTrue();
        config.MinimizeToTray.Should().BeTrue();
        config.ReactionDelayMs.Should().Be(100);
        config.DefaultAction.Should().Be(RuleAction.DisableGrayscale);
        config.Rules.Should().NotBeEmpty();  // Contains example rules
    }
}
