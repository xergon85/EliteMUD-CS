using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Server;

public class PortalStateTests
{
    [Fact]
    public void FromValues_ReadsLegacyPortalValues()
    {
        // Legacy 12037 Circus Maximus entrance: [dest, flags, key, min, max, hours]
        var portal = PortalState.FromValues(new[] { 12070, 1, 12038, -1, -1, -1 });

        Assert.Equal(12070, portal.Destination);
        Assert.True(portal.IsClosed);
        Assert.False(portal.IsLocked);
        Assert.Equal(12038, portal.KeyObjectId);
        Assert.False(portal.IsTooLowLevel(1));
        Assert.False(portal.IsTooHighLevel(109));
    }

    [Fact]
    public void LevelLimits_FollowMinAndMaxValues()
    {
        // Legacy 2906 "Legends of the Were-Folk": levels 50-80
        var portal = PortalState.FromValues(new[] { 12700, 2, 2904, 50, 80, -1 });

        Assert.True(portal.IsTooLowLevel(49));
        Assert.False(portal.IsTooLowLevel(50));
        Assert.False(portal.IsTooHighLevel(80));
        Assert.True(portal.IsTooHighLevel(81));
        Assert.True(portal.IsLocked);
    }

    [Fact]
    public void TickHour_ClosesOpenPortalWhenTimeRunsOut()
    {
        var portal = PortalState.FromValues(new[] { 100, 0, -1, -1, -1, 2 });

        Assert.False(portal.TickHour());
        Assert.True(portal.TickHour());
        Assert.True(portal.IsClosed);
        Assert.Equal(2, portal.HoursOpenRemaining);
    }

    [Fact]
    public void TickHour_NeverClosesPortalWithoutTimeout()
    {
        var portal = PortalState.FromValues(new[] { 100, 0, -1, -1, -1, -1 });

        for (var hour = 0; hour < 50; hour++)
        {
            Assert.False(portal.TickHour());
        }

        Assert.False(portal.IsClosed);
    }
}
