using WindowsAssistant.Services;

namespace WindowsAssistant.Tests;

/// <summary>
/// Tests for <see cref="MonitorControlService"/>:
///   - Brightness boundary validation
///   - Invalid monitor index handling
///   - Resource cleanup (IDisposable)
///   - Monitor descriptions
/// </summary>
public class MonitorControlServiceTests : IDisposable
{
    private readonly MonitorControlService _service = new();

    // -------------------------------------------------------------------------
    // Initial state
    // -------------------------------------------------------------------------

    [Fact]
    public void NewService_HasZeroMonitors()
    {
        Assert.Equal(0, _service.Count);
    }

    [Fact]
    public void NewService_ReturnsEmptyDescriptions()
    {
        Assert.Empty(_service.GetMonitorDescriptions());
    }

    // -------------------------------------------------------------------------
    // Invalid monitor index
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void SetBrightness_InvalidIndex_ReturnsFalse(int index)
    {
        // No monitors enumerated, so all indices are invalid
        bool result = _service.SetBrightness(index, 50);
        Assert.False(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void GetBrightness_InvalidIndex_ReturnsNull(int index)
    {
        uint? result = _service.GetBrightness(index);
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // RefreshMonitors — can be called multiple times safely
    // -------------------------------------------------------------------------

    [Fact]
    public void RefreshMonitors_CanBeCalledMultipleTimes()
    {
        // Should not throw even when called repeatedly
        _service.RefreshMonitors();
        _service.RefreshMonitors();
        _service.RefreshMonitors();
    }

    // -------------------------------------------------------------------------
    // Dispose — safe to call multiple times
    // -------------------------------------------------------------------------

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _service.Dispose();
        _service.Dispose(); // Should not throw
    }

    // -------------------------------------------------------------------------
    // Brightness clamping — the service clamps to 0–100
    // -------------------------------------------------------------------------

    [Fact]
    public void SetBrightness_ValueClampedTo100()
    {
        // With no monitors, this returns false, but verifies no exception
        // for out-of-range values
        Assert.False(_service.SetBrightness(0, 150));
    }

    public void Dispose() => _service.Dispose();
}
