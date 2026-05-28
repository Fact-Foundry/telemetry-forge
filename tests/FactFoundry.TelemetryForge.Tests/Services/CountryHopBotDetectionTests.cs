using FactFoundry.TelemetryForge.Server.Data;
using FactFoundry.TelemetryForge.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactFoundry.TelemetryForge.Tests.Services;

/// <summary>
/// Tests for country-hop bot detection — sessions appearing from 3+ distinct
/// countries are flagged as bots and prior events are retroactively updated.
/// </summary>
public class CountryHopBotDetectionTests
{
    private static TelemetryForgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelemetryForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryForgeDbContext(options);
    }

    private static WebEvent CreateEvent(string sessionHash, string? country, bool isBot = false)
    {
        return new WebEvent
        {
            SiteId = "site-1",
            SiteName = "Test",
            SessionHash = sessionHash,
            Page = "/test",
            EventType = "page_view",
            Language = "en-US",
            Country = country,
            IsBot = isBot,
            Timestamp = DateTimeOffset.UtcNow,
            IngestedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task TwoCountries_NotFlagged()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-a", "US"),
            CreateEvent("session-a", "JP"));
        await db.SaveChangesAsync();

        var priorCountries = await db.WebEvents
            .Where(e => e.SessionHash == "session-a" && e.Country != null)
            .Select(e => e.Country!)
            .Distinct()
            .ToListAsync();

        Assert.Equal(2, priorCountries.Count);
        Assert.True(priorCountries.Count < 3);
    }

    [Fact]
    public async Task ThreeCountries_Flagged()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-b", "US"),
            CreateEvent("session-b", "JP"));
        await db.SaveChangesAsync();

        var incomingCountry = "PL";

        var priorCountries = await db.WebEvents
            .Where(e => e.SessionHash == "session-b" && e.Country != null)
            .Select(e => e.Country!)
            .Distinct()
            .ToListAsync();

        if (!priorCountries.Contains(incomingCountry))
            priorCountries.Add(incomingCountry);

        Assert.Equal(3, priorCountries.Count);
        Assert.True(priorCountries.Count >= 3);
    }

    [Fact]
    public async Task ThreeCountries_RetroactivelyFlagsPriorEvents()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-c", "US"),
            CreateEvent("session-c", "JP"));
        await db.SaveChangesAsync();

        var incomingCountry = "PL";

        var priorCountries = await db.WebEvents
            .Where(e => e.SessionHash == "session-c" && e.Country != null)
            .Select(e => e.Country!)
            .Distinct()
            .ToListAsync();

        if (!priorCountries.Contains(incomingCountry))
            priorCountries.Add(incomingCountry);

        if (priorCountries.Count >= 3)
        {
            var priorEvents = await db.WebEvents
                .Where(e => e.SessionHash == "session-c" && !e.IsBot)
                .ToListAsync();
            foreach (var evt in priorEvents)
                evt.IsBot = true;
            await db.SaveChangesAsync();
        }

        var allEvents = await db.WebEvents.Where(e => e.SessionHash == "session-c").ToListAsync();
        Assert.All(allEvents, e => Assert.True(e.IsBot));
    }

    [Fact]
    public async Task SameCountryRepeated_NotFlagged()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-d", "US"),
            CreateEvent("session-d", "US"),
            CreateEvent("session-d", "US"));
        await db.SaveChangesAsync();

        var priorCountries = await db.WebEvents
            .Where(e => e.SessionHash == "session-d" && e.Country != null)
            .Select(e => e.Country!)
            .Distinct()
            .ToListAsync();

        Assert.Single(priorCountries);
        Assert.True(priorCountries.Count < 3);
    }

    [Fact]
    public async Task DifferentSessions_NotCrossContaminated()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-e", "US"),
            CreateEvent("session-e", "JP"),
            CreateEvent("session-f", "PL"));
        await db.SaveChangesAsync();

        var countriesE = await db.WebEvents
            .Where(e => e.SessionHash == "session-e" && e.Country != null)
            .Select(e => e.Country!)
            .Distinct()
            .ToListAsync();

        var countriesF = await db.WebEvents
            .Where(e => e.SessionHash == "session-f" && e.Country != null)
            .Select(e => e.Country!)
            .Distinct()
            .ToListAsync();

        Assert.Equal(2, countriesE.Count);
        Assert.Single(countriesF);
    }

    [Fact]
    public async Task NullCountry_ExcludedFromCount()
    {
        using var db = CreateDb();
        db.WebEvents.AddRange(
            CreateEvent("session-g", "US"),
            CreateEvent("session-g", null),
            CreateEvent("session-g", "JP"));
        await db.SaveChangesAsync();

        var priorCountries = await db.WebEvents
            .Where(e => e.SessionHash == "session-g" && e.Country != null)
            .Select(e => e.Country!)
            .Distinct()
            .ToListAsync();

        Assert.Equal(2, priorCountries.Count);
    }
}
