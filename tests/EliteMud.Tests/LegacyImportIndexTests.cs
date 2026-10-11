using EliteMud.Legacy.Import;

namespace EliteMud.Tests;

public class LegacyImportIndexTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"elitemud-index-{Guid.NewGuid():N}", "wld");

    public LegacyImportIndexTests()
    {
        Directory.CreateDirectory(_folder);
    }

    [Fact]
    public void Resolve_FollowsIndexOrder_ThenAddsUnindexedFiles()
    {
        WriteFiles("30.wld", "5.wld", "57.wld");
        File.WriteAllText(Path.Combine(_folder, "index"), "5.wld\n30.wld\n$~\n");

        var files = LegacyWorldFiles.Resolve(_folder, "wld", new LegacyImportConfig(), new LegacyImportLog());

        Assert.Equal(new[] { "wld/5.wld", "wld/30.wld", "wld/57.wld" }, files.Select(file => file.Name));
        Assert.Equal(new[] { true, true, false }, files.Select(file => file.IsIndexed));
    }

    [Fact]
    public void Resolve_SkipsFilesFromConfigAndLogsWhy()
    {
        WriteFiles("94.wld", "93.wld");
        File.WriteAllText(Path.Combine(_folder, "index"), "94.wld\n$\n");
        var config = new LegacyImportConfig
        {
            Skip = { new LegacyImportConfig.SkippedFile("wld/93.wld", "old copy of Galaxy") }
        };
        var log = new LegacyImportLog();

        var files = LegacyWorldFiles.Resolve(_folder, "wld", config, log);

        Assert.Equal("wld/94.wld", Assert.Single(files).Name);
        Assert.Contains(log.Messages, message => message.Source == "wld/93.wld" && message.Text == "old copy of Galaxy");
    }

    [Fact]
    public void RecordSet_LaterDefinitionWins_ButNeverAnUnloadedFileOverALoadedOne()
    {
        var log = new LegacyImportLog();
        var records = new LegacyRecordSet<string>("room", log);
        var first = new LegacySourceFile("a", "wld/0.wld", IsIndexed: true);
        var later = new LegacySourceFile("b", "wld/ocs.wld", IsIndexed: true);
        var unloaded = new LegacySourceFile("c", "wld/65.wld", IsIndexed: false);

        records.Add(12, "from 0.wld", first);
        records.Add(12, "from ocs.wld", later);
        records.Add(12, "from 65.wld", unloaded);

        Assert.Equal("from ocs.wld", Assert.Single(records.ToList()));
        Assert.Equal(2, log.Messages.Count(message => message.Category == "Duplicate id"));
    }

    [Fact]
    public void ZoneImporter_StopsAtEndMarkerZone()
    {
        // Legacy 98.zon ends with "#99" followed by "$~", which isn't a real zone 99
        var path = Path.Combine(_folder, "98.zon");
        File.WriteAllText(path, "#98\nRand's Tower~\n9899 65 1\nM 0 9800 1 9801\nS\n#99\n$~\n");
        var file = new LegacySourceFile(path, "zon/98.zon", IsIndexed: true);

        var zones = LegacyZoneImporter.Load(new[] { file }, new LegacyImportConfig(), new LegacyImportLog(), CancellationToken.None);

        var zone = Assert.Single(zones);
        Assert.Equal(98, zone.Id);
        Assert.True(zone.Active);
    }

    [Fact]
    public void ZoneImporter_UnindexedZone_IsInactiveUnlessConfiguredActive()
    {
        var path = Path.Combine(_folder, "57.zon");
        File.WriteAllText(path, "#57\nNew Thalos~\n5999 30 2\nS\n$~\n");
        var file = new LegacySourceFile(path, "zon/57.zon", IsIndexed: false);

        var inactive = LegacyZoneImporter.Load(new[] { file }, new LegacyImportConfig(), new LegacyImportLog(), CancellationToken.None);
        var active = LegacyZoneImporter.Load(new[] { file }, new LegacyImportConfig { ActiveZones = { 57 } }, new LegacyImportLog(), CancellationToken.None);

        Assert.False(Assert.Single(inactive).Active);
        Assert.True(Assert.Single(active).Active);
    }

    [Fact]
    public void MobImporter_ReadsResistanceSection()
    {
        // Shape of legacy 120.mob: an S-format mob followed by "R" and five values (-1 = unchanged)
        var path = Path.Combine(_folder, "120.mob");
        File.WriteAllText(path, """
            #12025
            lion~
            a hungry lion~
            A hungry lion is here.
            ~
            It looks hungry.
            ~
            1 0 g k 0 S
            25 0 -5 6d6+500
            400 500 85 4d7+3 -1
            -1
            -1
            425 145000 8 8 1
            R
            -1 -1 -1 150 5
            $~
            """);
        var file = new LegacySourceFile(path, "mob/120.mob", IsIndexed: true);
        var log = new LegacyImportLog();

        var mob = Assert.Single(LegacyMobImporter.Load(new[] { file }, log, CancellationToken.None));

        Assert.Contains("Resist_3:100", mob.Resistances); // capped at 100
        Assert.Contains("Resist_4:5", mob.Resistances);
        Assert.DoesNotContain(mob.Resistances, entry => entry.StartsWith("Resist_0:"));
        Assert.Empty(log.Messages);
    }

    private void WriteFiles(params string[] names)
    {
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(_folder, name), "$~\n");
        }
    }

    public void Dispose()
    {
        Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);
    }
}
