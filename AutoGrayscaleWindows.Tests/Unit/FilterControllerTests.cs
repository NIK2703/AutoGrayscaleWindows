using AutoGrayscaleWindows.Core;
using AutoGrayscaleWindows.Models;
using FluentAssertions;
using Microsoft.Win32;
using Xunit;

namespace AutoGrayscaleWindows.Tests.Unit;

/// <summary>
/// Unit-тесты для FilterController
/// </summary>
public class FilterControllerTests : IDisposable
{
    private readonly FilterController _filterController;
    private readonly string _registryKeyPath = @"Software\Microsoft\ColorFiltering";
    private readonly int _originalActive;
    private readonly int _originalFilterType;

    public FilterControllerTests()
    {
        _filterController = new FilterController();
        
        // Сохраняем оригинальные значения реестра
        using var key = Registry.CurrentUser.OpenSubKey(_registryKeyPath, true) 
                        ?? Registry.CurrentUser.CreateSubKey(_registryKeyPath, true);
        
        _originalActive = (int?)key.GetValue("Active", 0) ?? 0;
        _originalFilterType = (int?)key.GetValue("FilterType", 0) ?? 0;
    }

    public void Dispose()
    {
        // Восстанавливаем оригинальные значения реестра
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_registryKeyPath, true);
            if (key != null)
            {
                key.SetValue("Active", _originalActive);
                key.SetValue("FilterType", _originalFilterType);
            }
        }
        catch
        {
            // Игнорируем ошибки восстановления
        }
        
        _filterController.Dispose();
    }

    [Fact]
    public void IsGrayscaleEnabled_WhenActiveIs1_ReturnsTrue()
    {
        // Arrange
        SetRegistryValues(active: 1, filterType: 0);

        // Act
        var isEnabled = _filterController.IsGrayscaleEnabled;

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsGrayscaleEnabled_WhenActiveIs0_ReturnsFalse()
    {
        // Arrange
        SetRegistryValues(active: 0, filterType: 0);

        // Act
        var isEnabled = _filterController.IsGrayscaleEnabled;

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public void EnableGrayscale_SetsRegistryValues()
    {
        // Arrange
        SetRegistryValues(active: 0, filterType: 0);

        // Act
        var result = _filterController.EnableGrayscale();

        // Assert
        result.Should().BeTrue();
        _filterController.IsGrayscaleEnabled.Should().BeTrue();
        
        var (active, filterType) = GetRegistryValues();
        active.Should().Be(1);
        filterType.Should().Be(0);  // Grayscale
    }

    [Fact]
    public void DisableGrayscale_SetsRegistryValues()
    {
        // Arrange
        SetRegistryValues(active: 1, filterType: 0);

        // Act
        var result = _filterController.DisableGrayscale();

        // Assert
        result.Should().BeTrue();
        _filterController.IsGrayscaleEnabled.Should().BeFalse();
        
        var (active, _) = GetRegistryValues();
        active.Should().Be(0);
    }

    [Fact]
    public void ToggleGrayscale_ChangesState()
    {
        // Arrange
        SetRegistryValues(active: 0, filterType: 0);
        var initialState = _filterController.IsGrayscaleEnabled;

        // Act
        _filterController.ToggleGrayscale();

        // Assert
        _filterController.IsGrayscaleEnabled.Should().Be(!initialState);
    }

    [Fact]
    public void ToggleGrayscale_Twice_ReturnsToOriginalState()
    {
        // Arrange
        SetRegistryValues(active: 0, filterType: 0);
        var initialState = _filterController.IsGrayscaleEnabled;

        // Act
        _filterController.ToggleGrayscale();
        _filterController.ToggleGrayscale();

        // Assert
        _filterController.IsGrayscaleEnabled.Should().Be(initialState);
    }

    [Fact]
    public void Pause_SetsIsPausedToTrue()
    {
        // Arrange & Act
        _filterController.Pause();

        // Assert
        _filterController.IsPaused.Should().BeTrue();
    }

    [Fact]
    public void Resume_SetsIsPausedToFalse()
    {
        // Arrange
        _filterController.Pause();

        // Act
        _filterController.Resume();

        // Assert
        _filterController.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void GetFilterState_ReturnsCurrentState()
    {
        // Arrange
        SetRegistryValues(active: 1, filterType: 0);

        // Act
        var isActive = _filterController.IsGrayscaleEnabled;

        // Assert
        isActive.Should().BeTrue();
    }

    [Fact]
    public void SyncState_UpdatesInternalState()
    {
        // Arrange
        SetRegistryValues(active: 1, filterType: 0);

        // Act
        _filterController.SyncState();

        // Assert
        _filterController.IsGrayscaleEnabled.Should().BeTrue();
    }

    [Fact]
    public void FilterChanged_EventIsRaisedOnEnable()
    {
        // Arrange
        SetRegistryValues(active: 0, filterType: 0);
        var eventRaised = false;
        _filterController.FilterChanged += (_, _) => eventRaised = true;

        // Act
        _filterController.EnableGrayscale();

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void FilterChanged_EventIsRaisedOnDisable()
    {
        // Arrange
        SetRegistryValues(active: 1, filterType: 0);
        var eventRaised = false;
        _filterController.FilterChanged += (_, _) => eventRaised = true;

        // Act
        _filterController.DisableGrayscale();

        // Assert
        eventRaised.Should().BeTrue();
    }

    #region Helper Methods

    private void SetRegistryValues(int active, int filterType)
    {
        using var key = Registry.CurrentUser.OpenSubKey(_registryKeyPath, true) 
                        ?? Registry.CurrentUser.CreateSubKey(_registryKeyPath, true);
        
        key.SetValue("Active", active, RegistryValueKind.DWord);
        key.SetValue("FilterType", filterType, RegistryValueKind.DWord);
    }

    private (int Active, int FilterType) GetRegistryValues()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_registryKeyPath, false);
        
        if (key == null)
            return (0, 0);
        
        var active = (int?)key.GetValue("Active", 0) ?? 0;
        var filterType = (int?)key.GetValue("FilterType", 0) ?? 0;
        
        return (active, filterType);
    }

    #endregion
}
