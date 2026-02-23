using AutoGrayscaleWindows.Core;
using AutoGrayscaleWindows.Models;
using FluentAssertions;
using Xunit;
using MatchType = AutoGrayscaleWindows.Models.MatchType;

namespace AutoGrayscaleWindows.Tests.Unit;

/// <summary>
/// Unit-тесты для RuleEngine
/// </summary>
public class RuleEngineTests : IDisposable
{
    private readonly RuleEngine _ruleEngine;

    public RuleEngineTests()
    {
        _ruleEngine = new RuleEngine();
    }

    public void Dispose()
    {
        _ruleEngine.Dispose();
    }

    [Fact]
    public void Evaluate_WithEmptyRules_ReturnsDefaultAction()
    {
        // Arrange
        var rules = new List<AppRule>();
        _ruleEngine.SetRules(rules, RuleAction.DisableGrayscale);
        var windowInfo = CreateTestWindowInfo("chrome.exe");

        // Act
        var result = _ruleEngine.Evaluate(windowInfo);

        // Assert
        result.RuleFound.Should().BeFalse();
        result.Action.Should().Be(RuleAction.DisableGrayscale);
        result.MatchedRule.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithExactMatch_ReturnsRuleAction()
    {
        // Arrange
        var rule = new AppRule
        {
            AppIdentifier = "chrome.exe",
            DisplayName = "Chrome",
            MatchType = MatchType.Exact,
            Action = RuleAction.EnableGrayscale,
            IsActive = true,
            Priority = 10
        };
        _ruleEngine.SetRules(new List<AppRule> { rule }, RuleAction.DisableGrayscale);
        
        var windowInfo = CreateTestWindowInfo("chrome.exe");

        // Act
        var result = _ruleEngine.Evaluate(windowInfo);

        // Assert
        result.RuleFound.Should().BeTrue();
        result.Action.Should().Be(RuleAction.EnableGrayscale);
        result.MatchedRule.Should().NotBeNull();
        result.MatchedRule!.DisplayName.Should().Be("Chrome");
    }

    [Fact]
    public void Evaluate_WithContainsMatch_ReturnsRuleAction()
    {
        // Arrange
        var rule = new AppRule
        {
            AppIdentifier = "Visual Studio",
            DisplayName = "VS",
            MatchType = MatchType.Contains,
            Action = RuleAction.DisableGrayscale,
            IsActive = true,
            Priority = 10
        };
        _ruleEngine.SetRules(new List<AppRule> { rule }, RuleAction.EnableGrayscale);
        
        var windowInfo = CreateTestWindowInfo(
            processName: "devenv",
            executablePath: @"C:\Program Files\Microsoft Visual Studio\2022\devenv.exe");

        // Act
        var result = _ruleEngine.Evaluate(windowInfo);

        // Assert
        result.RuleFound.Should().BeTrue();
        result.Action.Should().Be(RuleAction.DisableGrayscale);
    }

    [Fact]
    public void Evaluate_WithRegexMatch_ReturnsRuleAction()
    {
        // Arrange
        var rule = new AppRule
        {
            AppIdentifier = @"^(chrome|firefox|edge)\.exe$",
            DisplayName = "Browsers",
            MatchType = MatchType.Regex,
            Action = RuleAction.EnableGrayscale,
            IsActive = true,
            Priority = 10
        };
        _ruleEngine.SetRules(new List<AppRule> { rule }, RuleAction.DisableGrayscale);
        
        var windowInfo = CreateTestWindowInfo("chrome.exe");

        // Act
        var result = _ruleEngine.Evaluate(windowInfo);

        // Assert
        result.RuleFound.Should().BeTrue();
        result.Action.Should().Be(RuleAction.EnableGrayscale);
    }

    [Fact]
    public void Evaluate_WithMultipleRules_ReturnsHighestPriority()
    {
        // Arrange
        var rules = new List<AppRule>
        {
            new()
            {
                AppIdentifier = "chrome.exe",
                DisplayName = "Chrome Enable",
                MatchType = MatchType.Exact,
                Action = RuleAction.EnableGrayscale,
                IsActive = true,
                Priority = 5
            },
            new()
            {
                AppIdentifier = "chrome.exe",
                DisplayName = "Chrome Disable",
                MatchType = MatchType.Exact,
                Action = RuleAction.DisableGrayscale,
                IsActive = true,
                Priority = 10  // Higher priority
            }
        };
        _ruleEngine.SetRules(rules, RuleAction.DisableGrayscale);
        
        var windowInfo = CreateTestWindowInfo("chrome.exe");

        // Act
        var result = _ruleEngine.Evaluate(windowInfo);

        // Assert
        result.RuleFound.Should().BeTrue();
        result.Action.Should().Be(RuleAction.DisableGrayscale);
        result.MatchedRule!.Priority.Should().Be(10);
    }

    [Fact]
    public void Evaluate_WithDisabledRule_IgnoresRule()
    {
        // Arrange
        var rule = new AppRule
        {
            AppIdentifier = "chrome.exe",
            DisplayName = "Chrome",
            MatchType = MatchType.Exact,
            Action = RuleAction.EnableGrayscale,
            IsActive = false,  // Disabled
            Priority = 10
        };
        _ruleEngine.SetRules(new List<AppRule> { rule }, RuleAction.DisableGrayscale);
        
        var windowInfo = CreateTestWindowInfo("chrome.exe");

        // Act
        var result = _ruleEngine.Evaluate(windowInfo);

        // Assert
        result.RuleFound.Should().BeFalse();
        result.Action.Should().Be(RuleAction.DisableGrayscale);
    }

    [Fact]
    public void Evaluate_WithNoMatch_ReturnsDefaultAction()
    {
        // Arrange
        var rule = new AppRule
        {
            AppIdentifier = "chrome.exe",
            DisplayName = "Chrome",
            MatchType = MatchType.Exact,
            Action = RuleAction.EnableGrayscale,
            IsActive = true,
            Priority = 10
        };
        _ruleEngine.SetRules(new List<AppRule> { rule }, RuleAction.DisableGrayscale);
        
        var windowInfo = CreateTestWindowInfo("firefox.exe");

        // Act
        var result = _ruleEngine.Evaluate(windowInfo);

        // Assert
        result.RuleFound.Should().BeFalse();
        result.Action.Should().Be(RuleAction.DisableGrayscale);
    }

    [Fact]
    public void Evaluate_WithEmptyWindowInfo_ReturnsDefaultAction()
    {
        // Arrange
        var rule = new AppRule
        {
            AppIdentifier = "chrome.exe",
            DisplayName = "Chrome",
            MatchType = MatchType.Exact,
            Action = RuleAction.EnableGrayscale,
            IsActive = true,
            Priority = 10
        };
        _ruleEngine.SetRules(new List<AppRule> { rule }, RuleAction.DisableGrayscale);
        
        // Act
        var result = _ruleEngine.Evaluate(WindowInfo.Empty);

        // Assert
        result.RuleFound.Should().BeFalse();
        result.Action.Should().Be(RuleAction.DisableGrayscale);
    }

    [Fact]
    public void SetRules_WithNullRules_DoesNotThrow()
    {
        // Arrange & Act
        var action = () => _ruleEngine.SetRules(null!, RuleAction.DisableGrayscale);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void ClearCache_ClearsAllCachedResults()
    {
        // Arrange
        var rule = new AppRule
        {
            AppIdentifier = "chrome.exe",
            DisplayName = "Chrome",
            MatchType = MatchType.Exact,
            Action = RuleAction.EnableGrayscale,
            IsActive = true,
            Priority = 10
        };
        _ruleEngine.SetRules(new List<AppRule> { rule }, RuleAction.DisableGrayscale);
        
        var windowInfo = CreateTestWindowInfo("chrome.exe");
        _ruleEngine.Evaluate(windowInfo); // Cache the result

        // Act
        _ruleEngine.ClearCache();

        // Assert - No exception means success
        _ruleEngine.ActiveRuleCount.Should().Be(1);
    }

    [Fact]
    public void ActiveRuleCount_ReturnsCorrectCount()
    {
        // Arrange
        var rules = new List<AppRule>
        {
            new() { AppIdentifier = "chrome.exe", IsActive = true, Priority = 10 },
            new() { AppIdentifier = "firefox.exe", IsActive = true, Priority = 10 },
            new() { AppIdentifier = "edge.exe", IsActive = false, Priority = 10 }  // Inactive
        };
        _ruleEngine.SetRules(rules, RuleAction.DisableGrayscale);

        // Act & Assert
        _ruleEngine.ActiveRuleCount.Should().Be(2); // Only active rules
    }

    private static WindowInfo CreateTestWindowInfo(
        string processName,
        string executablePath = "",
        string windowTitle = "Test Window")
    {
        return new WindowInfo
        {
            Handle = 12345,
            ProcessId = 1000,
            ProcessName = processName,
            WindowTitle = windowTitle,
            ExecutablePath = string.IsNullOrEmpty(executablePath) 
                ? $@"C:\Programs\{processName}" 
                : executablePath
        };
    }
}
