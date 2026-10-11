using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EliteMud.Legacy.Import;

/// <summary>
/// Extracts the class, race, XP curve and level-up rules from the legacy C source into content files.
/// The tables are C arrays (constants.c), and the level-up rules are switch statements (limits.c), so this reads
/// the source code itself. Every decision is logged with the source line it came from.
/// </summary>
public sealed partial class LegacyRulesImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Ids for the shared XP curves and level-up profiles, by the first class in each group.
    // Legacy has no names for these groups; these follow the usual MUD archetypes.
    private static readonly Dictionary<string, string> GroupNames = new(StringComparer.Ordinal)
    {
        ["magic-user"] = "mage",
        ["cleric"] = "priest",
        ["thief"] = "rogue",
        ["warrior"] = "fighter",
        ["bard"] = "bard",
        ["assassin"] = "assassin"
    };

    // Variable names in improve_abilities() -> stat ids
    private static readonly Dictionary<string, string> StatNames = new(StringComparer.Ordinal)
    {
        ["str"] = "str", ["intel"] = "int", ["wis"] = "wis", ["dex"] = "dex", ["con"] = "con", ["cha"] = "cha"
    };

    /// <summary>
    /// The import decisions (race availability) in the target content tree, so a rerun from any folder gives the
    /// same output. Without the file every race outside the creation menu comes out as unknown, so say so.
    /// </summary>
    private static LegacyImportConfig LoadConfig(string contentPath, LegacyImportLog log)
    {
        var path = Path.Combine(contentPath, "meta", "legacy-import.json");
        if (File.Exists(path))
        {
            log.Info("Config", "meta/legacy-import.json", "import decisions loaded");
            return LegacyImportConfig.Load(path);
        }

        log.Warn("Config", "meta/legacy-import.json", $"not found in {contentPath}; races outside the creation menu will be 'unknown'");
        return new LegacyImportConfig();
    }

    /// <summary>
    /// Read the legacy source under <paramref name="legacyRoot"/> (its src/ folder) and write the rules content
    /// under <paramref name="contentPath"/>: classes/, races/, progression/xp-curves/ and progression/level-up/.
    /// <paramref name="config"/> defaults to <paramref name="contentPath"/>/meta/legacy-import.json.
    /// </summary>
    public async ValueTask<LegacyImportLog> ImportAsync(
        string legacyRoot,
        string contentPath,
        CancellationToken cancellationToken,
        LegacyImportConfig? config = null)
    {
        var log = new LegacyImportLog();
        config ??= LoadConfig(contentPath, log);
        var source = new LegacySource(ResolveSourcePath(legacyRoot));
        var defines = ReadDefines(source.Read("structs.h"));
        var maxMortalLevel = defines["LEVEL_DEITY"] - 1;

        var classNames = ReadStrings(source, "constants.c", "pc_class_types", log);
        var classes = ReadClasses(source, defines, classNames, log);
        var xpCurves = BuildXpCurves(classes, maxMortalLevel, log);
        var profiles = ReadLevelUpProfiles(source, defines, classNames, log);
        var primeStats = ReadPrimeStats(source, defines, classNames, log);
        var races = ReadRaces(source, defines, classNames, classes, config, log);

        var classContents = classes.Values
            .OrderBy(c => c.Number)
            .Select(c => new ClassRulesContent(
                c.Id,
                c.Name,
                c.Abbreviation,
                c.Number,
                xpCurves.Single(curve => curve.Classes.Contains(c.Id)).Id,
                profiles.SingleOrDefault(profile => profile.Classes.Contains(c.Id))?.Id
                    ?? throw new InvalidDataException($"class {c.Id} is in no advance_level() case"),
                primeStats.TryGetValue(c.Id, out var prime) ? prime.Stats : Array.Empty<string>(),
                prime?.StrengthBonus ?? false,
                c.Titles))
            .ToList();

        await WriteAllAsync(Path.Combine(contentPath, "classes"), classContents, c => c.Id, log, cancellationToken);
        await WriteAllAsync(Path.Combine(contentPath, "races"), races, r => r.Id, log, cancellationToken);
        await WriteAllAsync(Path.Combine(contentPath, "progression", "xp-curves"), xpCurves, c => c.Id, log, cancellationToken);
        await WriteAllAsync(Path.Combine(contentPath, "progression", "level-up"), profiles, p => p.Id, log, cancellationToken);

        Directory.CreateDirectory(Path.Combine(contentPath, "meta"));
        await log.SaveAsync(Path.Combine(contentPath, "meta", "legacy-rules-import-log.txt"), cancellationToken);
        return log;
    }

    private sealed record LegacyClass(int Number, string Id, string Name, string Abbreviation,
        IReadOnlyList<TitleContent> Titles, IReadOnlyList<int> XpRows);

    private sealed record PrimeStats(IReadOnlyList<string> Stats, bool StrengthBonus);

    /// <summary>Classes 1-20 from titles[20][40]; the unused slots and dual/multi-class are left out.</summary>
    private static Dictionary<string, LegacyClass> ReadClasses(
        LegacySource source, Dictionary<string, int> defines, IReadOnlyList<string> classNames, LegacyImportLog log)
    {
        var abbreviations = ReadStrings(source, "constants.c", "class_abbrevs", log);
        var (body, line) = source.ArrayBody("constants.c", "titles");
        var rows = TitleRows(body);
        var classes = new Dictionary<string, LegacyClass>(StringComparer.Ordinal);

        for (var number = 1; number < classNames.Count; number++)
        {
            var id = classNames[number];
            if (number > rows.Count)
            {
                log.Info("Class skipped", $"pc_class_types[{number}]", $"{id}: dual/multi-class, deferred (see the levelling plan)");
                continue;
            }

            var (name, titles) = rows[number - 1];
            if (id == "DO_NOT_USE")
            {
                log.Info("Class skipped", $"constants.c:{line} titles[{number - 1}]", $"unused class slot ({name})");
                continue;
            }

            if (titles.Count != 40)
            {
                throw new InvalidDataException($"titles[{number - 1}] ({name}) has {titles.Count} rows, expected 40");
            }

            classes[id] = new LegacyClass(
                number,
                id,
                name,
                abbreviations[number].Trim(),
                titles.Select(t => new TitleContent(t.Male, t.Female)).ToList(),
                titles.Select(t => t.Exp).ToList());
            log.Info("Class", $"constants.c:{line} titles[{number - 1}]", $"{id} = {name}, 40 title rows");
        }

        return classes;
    }

    /// <summary>
    /// Group classes with identical XP rows into shared curves, with the XP needed per level 1-109 computed like
    /// exp_needed() in limits.c: one legacy row per 3 levels, interpolated with C integer division.
    /// </summary>
    private static List<XpCurveContent> BuildXpCurves(Dictionary<string, LegacyClass> classes, int maxMortalLevel, LegacyImportLog log)
    {
        var curves = new List<XpCurveContent>();
        foreach (var group in classes.Values.OrderBy(c => c.Number).GroupBy(c => string.Join(",", c.XpRows)))
        {
            var members = group.Select(c => c.Id).ToList();
            var rows = group.First().XpRows;
            var id = GroupNames.TryGetValue(members[0], out var name) ? name : members[0];
            if (name is null)
            {
                log.Warn("XP curve", "titles[][]", $"no group name for {members[0]}; using its class id");
            }

            var thresholds = Enumerable.Range(1, maxMortalLevel).Select(level => ExpNeeded(rows, level, maxMortalLevel)).ToList();
            curves.Add(new XpCurveContent(id, members, thresholds, rows));
            log.Info("XP curve", "limits.c exp_needed()", $"{id}: {string.Join(", ", members)} (level 1 needs {thresholds[0]}, level {maxMortalLevel} needs {thresholds[^1]})");
        }

        return curves;
    }

    /// <summary>Legacy exp_needed(): needed = rows[lv/3+1] + (diff/3) * (lv%3), integer math.</summary>
    public static int ExpNeeded(IReadOnlyList<int> rows, int level, int maxMortalLevel)
    {
        var lv = Math.Min(level, maxMortalLevel);
        var needed = rows[lv / 3 + 1];
        var diff = rows[lv / 3 + 2] - needed;
        return needed + diff / 3 * (lv % 3);
    }

    /// <summary>
    /// Level-up gains from advance_level() in limits.c: a switch over the class with "add_hp += GET_CON(ch)/N"
    /// and "add_mana = M" per group of cases.
    /// </summary>
    private static List<LevelUpProfileContent> ReadLevelUpProfiles(
        LegacySource source, Dictionary<string, int> defines, IReadOnlyList<string> classNames, LegacyImportLog log)
    {
        var (function, functionLine) = source.Function("limits.c", "advance_level");
        var hpDie = HpDie().Match(function);
        var move = MoveGain().Match(function);
        var noManaAtLevelOne = function.Contains("GET_LEVEL(ch) != 1", StringComparison.Ordinal);
        if (!hpDie.Success || !move.Success)
        {
            throw new InvalidDataException("advance_level(): couldn't find the hp die or the move gain");
        }

        var profiles = new List<LevelUpProfileContent>();
        foreach (var (members, body) in CaseGroups(Switch(function, "LOWEST_CLASS(ch)"), defines, classNames))
        {
            var conDivisor = ConDivisor().Match(body);
            var mana = ManaGain().Match(body);
            if (!conDivisor.Success || !mana.Success)
            {
                throw new InvalidDataException($"advance_level(): no hp/mana gain for {string.Join(", ", members)}");
            }

            var id = GroupNames.TryGetValue(members[0], out var name) ? name : members[0];
            var manaFormula = noManaAtLevelOne
                ? $"if level == 1 then return 0 end return {mana.Groups[1].Value}"
                : $"return {mana.Groups[1].Value}";
            profiles.Add(new LevelUpProfileContent(
                id,
                members,
                $"return math.max(1, random({hpDie.Groups[1].Value}, {hpDie.Groups[2].Value}) + div(con, {conDivisor.Groups[1].Value}))",
                manaFormula,
                $"return {move.Groups[1].Value}",
                $"limits.c:{functionLine} advance_level(); 'level' is the level before advancing, as in legacy"));
            log.Info("Level-up profile", $"limits.c:{functionLine} advance_level()",
                $"{id}: {string.Join(", ", members)}: hp 1d5 + con/{conDivisor.Groups[1].Value}, mana {mana.Groups[1].Value}");
        }

        return profiles;
    }

    /// <summary>
    /// Favored stats from improve_abilities() in limits.c: each class group sets its favored stats' threshold to 0
    /// (they improve more often); fighters also get the strength bonus (stradd).
    /// </summary>
    private static Dictionary<string, PrimeStats> ReadPrimeStats(
        LegacySource source, Dictionary<string, int> defines, IReadOnlyList<string> classNames, LegacyImportLog log)
    {
        var (function, functionLine) = source.Function("limits.c", "improve_abilities");
        var result = new Dictionary<string, PrimeStats>(StringComparer.Ordinal);
        foreach (var (members, body) in CaseGroups(Switch(function, "class"), defines, classNames))
        {
            var stats = ZeroedStat().Matches(body).Select(m => StatNames[m.Groups[1].Value]).ToList();
            var strengthBonus = body.Contains("stradd = TRUE", StringComparison.Ordinal);
            foreach (var member in members)
            {
                result[member] = new PrimeStats(stats, strengthBonus);
            }

            log.Info("Favored stats", $"limits.c:{functionLine} improve_abilities()",
                $"{string.Join(", ", members)}: {string.Join(", ", stats)}{(strengthBonus ? " + strength bonus" : "")}");
        }

        return result;
    }

    /// <summary>Races from the per-race tables in constants.c; playable ones from the creation menu in interpreter.c.</summary>
    private static List<RaceRulesContent> ReadRaces(
        LegacySource source,
        Dictionary<string, int> defines,
        IReadOnlyList<string> classNames,
        Dictionary<string, LegacyClass> classes,
        LegacyImportConfig config,
        LegacyImportLog log)
    {
        var names = ReadStrings(source, "constants.c", "race_table", log);
        var titleNames = ReadStrings(source, "constants.c", "race_title_names", log);
        int[] Ints(string array) => PerRace(ReadInts(source, "constants.c", array), array, names, log);
        var (str, con, dex, intel, wis, cha) = (Ints("str_max"), Ints("con_max"), Ints("dex_max"), Ints("int_max"), Ints("wis_max"), Ints("cha_max"));
        var (hp, mana, move) = (Ints("hp_start"), Ints("mn_start"), Ints("mv_start"));
        var (age, height, weight) = (Ints("age_average"), Ints("height_average"), Ints("weight_average"));
        var allowed = PerRace(ReadAllowedClasses(source, defines).ToArray(), "allowed_classes", names, log);

        var (menu, menuLine) = source.Find("interpreter.c", RaceMenuCase());
        var playable = menu.Select(m => defines[m.Groups[1].Value]).ToHashSet();
        log.Info("Playable races", $"interpreter.c:{menuLine}", $"{playable.Count} races in the creation menu");

        var races = new List<RaceRulesContent>();
        for (var number = 0; number < names.Count; number++)
        {
            var id = names[number];
            var allowedIds = new List<string>();
            for (var bit = 0; bit < 32; bit++)
            {
                if ((allowed[number] & (1 << bit)) == 0)
                {
                    continue;
                }

                // Bit b is class b + 1 (the *_CLASS macros in structs.h; NE_CLASS, bit 4, is the psionicist)
                var classId = bit + 1 < classNames.Count ? classNames[bit + 1] : $"#{bit + 1}";
                if (classes.ContainsKey(classId))
                {
                    allowedIds.Add(classId);
                }
                else
                {
                    log.Info("Allowed class skipped", $"constants.c allowed_classes[{number}]", $"{id}: {classId} (deferred or unused)");
                }
            }

            var (availability, note) = Availability(id, playable.Contains(number), config, log);
            races.Add(new RaceRulesContent(
                id,
                char.ToUpperInvariant(id[0]) + id[1..],
                titleNames[number],
                number,
                availability,
                note,
                new StatBlockContent(str[number], intel[number], wis[number], dex[number], con[number], cha[number]),
                new StartingPointsContent(hp[number], mana[number], move[number]),
                allowedIds,
                new AveragesContent(age[number], height[number], weight[number])));
            log.Info("Race", $"constants.c race_table[{number}]", $"{id} ({availability}): {allowedIds.Count} allowed classes");
        }

        foreach (var entry in config.RaceAvailability.Where(entry => !names.Contains(entry.Race)))
        {
            log.Warn("Race availability", "legacy-import.json", $"no race '{entry.Race}' in race_table; entry ignored");
        }

        return races;
    }

    /// <summary>
    /// "creation" for races in the creation menu; otherwise the config's entry (granted, remort, unused) with its
    /// note and source; otherwise "unknown".
    /// </summary>
    private static (string Availability, string? Note) Availability(string race, bool inMenu, LegacyImportConfig config, LegacyImportLog log)
    {
        var entry = config.RaceAvailability.FirstOrDefault(e => string.Equals(e.Race, race, StringComparison.Ordinal));
        if (inMenu)
        {
            if (entry is not null)
            {
                log.Warn("Race availability", "legacy-import.json", $"{race} is in the creation menu; its entry ({entry.Availability}) is ignored");
            }

            return ("creation", null);
        }

        if (entry is null)
        {
            log.Info("Race availability", $"race {race}", "not in the creation menu and not in legacy-import.json: unknown");
            return ("unknown", null);
        }

        log.Info("Race availability", $"race {race}", $"{entry.Availability}: {entry.Note} (source: {entry.Source})");
        return (entry.Availability, entry.Note);
    }

    /// <summary>
    /// Check a per-race table has one entry per race. A shorter table is padded with 0 and logged: legacy would read
    /// past the end of the array (allowed_classes[] has no entry for the last race, angel, which isn't playable).
    /// </summary>
    private static int[] PerRace(int[] values, string array, IReadOnlyList<string> races, LegacyImportLog log)
    {
        if (values.Length > races.Count)
        {
            throw new InvalidDataException($"constants.c {array} has {values.Length} entries for {races.Count} races");
        }

        for (var missing = values.Length; missing < races.Count; missing++)
        {
            log.Warn("Short race table", $"constants.c {array}", $"no entry for race {missing} ({races[missing]}); using 0");
        }

        return values.Concat(Enumerable.Repeat(0, races.Count - values.Length)).ToArray();
    }

    /// <summary>allowed_classes[]: one "A_CLASS | B_CLASS | ..." expression per race.</summary>
    private static List<int> ReadAllowedClasses(LegacySource source, Dictionary<string, int> defines)
    {
        var (body, _) = source.ArrayBody("constants.c", "allowed_classes");
        return SplitTopLevel(StripComments(body))
            .Where(element => element.Length > 0)
            .Select(element => element.Split('|', StringSplitOptions.TrimEntries)
                .Aggregate(0, (bits, name) => bits | (int.TryParse(name, out var number) ? number : defines[name])))
            .ToList();
    }

    private static List<string> ReadStrings(LegacySource source, string file, string array, LegacyImportLog log)
    {
        var (body, _) = source.ArrayBody(file, array);
        return QuotedString().Matches(StripComments(body))
            .Select(m => m.Groups[1].Value)
            .TakeWhile(value => value != "\\n") // legacy lists end with "\n"
            .ToList();
    }

    private static int[] ReadInts(LegacySource source, string file, string array)
    {
        var (body, _) = source.ArrayBody(file, array);
        return SplitTopLevel(StripComments(body)).Where(e => e.Length > 0).Select(int.Parse).ToArray();
    }

    /// <summary>
    /// titles[][]: one { ... } group of 40 { "male", "female", exp } rows per class, named by the comment before
    /// the group (closing "End ..." comments are ignored).
    /// </summary>
    private static List<(string Name, List<(string Male, string Female, int Exp)> Rows)> TitleRows(string body)
    {
        var classes = new List<(string, List<(string, string, int)>)>();
        foreach (var element in SplitTopLevel(body).Where(e => e.Contains('{')))
        {
            var open = element.IndexOf('{');
            var name = TitleComment().Matches(element[..open])
                .Select(m => m.Groups[1].Value.Trim())
                .LastOrDefault(comment => !comment.StartsWith("End ", StringComparison.Ordinal)) ?? "?";
            var rows = TitleRow().Matches(element[open..])
                .Select(m => (m.Groups["male"].Value, m.Groups["female"].Value, int.Parse(m.Groups["exp"].Value)))
                .ToList();
            classes.Add((name, rows));
        }

        return classes;
    }

    /// <summary>"case CLASS_A: case CLASS_B: ...body... break;" groups inside a switch body.</summary>
    private static IEnumerable<(List<string> Classes, string Body)> CaseGroups(
        string switchBody, Dictionary<string, int> defines, IReadOnlyList<string> classNames)
    {
        foreach (Match match in CaseGroup().Matches(switchBody))
        {
            var members = CaseLabel().Matches(match.Groups["cases"].Value)
                .Select(label => classNames[defines[label.Groups[1].Value]])
                .ToList();
            yield return (members, match.Groups["body"].Value);
        }
    }

    private static string Switch(string function, string expression)
    {
        var start = function.IndexOf($"switch ({expression})", StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidDataException($"no 'switch ({expression})' found");
        }

        var open = function.IndexOf('{', start);
        return function[(open + 1)..MatchingBrace(function, open)];
    }

    private static Dictionary<string, int> ReadDefines(string header)
    {
        var defines = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in Define().Matches(header))
        {
            defines[match.Groups["name"].Value] = match.Groups["shift"].Success
                ? 1 << int.Parse(match.Groups["shift"].Value)
                : int.Parse(match.Groups["value"].Value);
        }

        return defines;
    }

    private static async ValueTask WriteAllAsync<T>(
        string folder, IEnumerable<T> items, Func<T, string> id, LegacyImportLog log, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var path = Path.Combine(folder, $"{id(item)}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(item, JsonOptions) + "\n", cancellationToken);
            written.Add(Path.GetFileName(path));
        }

        foreach (var stale in Directory.EnumerateFiles(folder, "*.json").Select(Path.GetFileName).Where(f => !written.Contains(f!)))
        {
            log.Warn("Stale file", Path.Combine(Path.GetFileName(folder), stale!), "not produced by this import; remove it if it's obsolete");
        }
    }

    private static string ResolveSourcePath(string legacyRoot)
    {
        var src = Path.Combine(legacyRoot, "src");
        var path = File.Exists(Path.Combine(src, "constants.c")) ? src : legacyRoot;
        return File.Exists(Path.Combine(path, "constants.c"))
            ? path
            : throw new DirectoryNotFoundException($"constants.c not found under {legacyRoot} or {src}");
    }

    private static string StripComments(string code) => BlockComment().Replace(code, " ");

    private static IEnumerable<string> SplitTopLevel(string body)
    {
        var depth = 0;
        var current = new StringBuilder();
        foreach (var ch in body)
        {
            if (ch == '{') depth++;
            if (ch == '}') depth--;
            if (ch == ',' && depth == 0)
            {
                yield return current.ToString().Trim();
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        yield return current.ToString().Trim();
    }

    private static int MatchingBrace(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            if (text[i] == '}' && --depth == 0) return i;
        }

        throw new InvalidDataException("unbalanced braces");
    }

    /// <summary>Legacy source files (Latin-1), with helpers to find arrays and functions and their line numbers.</summary>
    private sealed class LegacySource(string path)
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public string Read(string file) =>
            _files.TryGetValue(file, out var text) ? text : _files[file] = File.ReadAllText(Path.Combine(path, file), Encoding.Latin1);

        public (string Body, int Line) ArrayBody(string file, string array)
        {
            var text = Read(file);
            var match = Regex.Match(text, $@"\b{array}\s*\[[^\]]*\](?:\s*\[[^\]]*\])?\s*=\s*\{{");
            if (!match.Success)
            {
                throw new InvalidDataException($"{file}: array {array} not found");
            }

            var open = match.Index + match.Length - 1;
            return (text[(open + 1)..MatchingBrace(text, open)], LineOf(text, match.Index));
        }

        public (string Body, int Line) Function(string file, string name)
        {
            var text = Read(file);
            var match = Regex.Match(text, $@"^\w[\w\s\*]*\b{name}\s*\([^)]*\)\s*\{{", RegexOptions.Multiline);
            if (!match.Success)
            {
                throw new InvalidDataException($"{file}: function {name} not found");
            }

            var open = match.Index + match.Length - 1;
            return (text[(open + 1)..MatchingBrace(text, open)], LineOf(text, match.Index));
        }

        public (IReadOnlyList<Match> Matches, int Line) Find(string file, Regex pattern)
        {
            var text = Read(file);
            var matches = pattern.Matches(text).ToList();
            return matches.Count == 0
                ? throw new InvalidDataException($"{file}: {pattern} not found")
                : (matches, LineOf(text, matches[0].Index));
        }

        private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;
    }

    [GeneratedRegex(@"#define\s+(?<name>\w+)\s+(?:\(\s*1\s*<<\s*(?<shift>\d+)\s*\)|(?<value>-?\d+))")]
    private static partial Regex Define();

    [GeneratedRegex(@"""((?:\\.|[^""\\])*)""")]
    private static partial Regex QuotedString();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"/\*([^*]*?)\*/")]
    private static partial Regex TitleComment();

    [GeneratedRegex(@"\{\s*""(?<male>(?:\\.|[^""\\])*)""\s*,\s*""(?<female>(?:\\.|[^""\\])*)""\s*,\s*(?<exp>\d+)\s*\}")]
    private static partial Regex TitleRow();

    [GeneratedRegex(@"(?<cases>(?:\s*case\s+CLASS_\w+\s*:)+)(?<body>.*?)\bbreak\s*;", RegexOptions.Singleline)]
    private static partial Regex CaseGroup();

    [GeneratedRegex(@"case\s+(CLASS_\w+)\s*:")]
    private static partial Regex CaseLabel();

    [GeneratedRegex(@"add_hp\s*=\s*number\(\s*(\d+)\s*,\s*(\d+)\s*\)")]
    private static partial Regex HpDie();

    [GeneratedRegex(@"add_hp\s*\+=\s*GET_CON\(ch\)\s*/\s*(\d+)")]
    private static partial Regex ConDivisor();

    [GeneratedRegex(@"add_mana\s*=\s*(\d+)")]
    private static partial Regex ManaGain();

    [GeneratedRegex(@"max_move\s*\+=\s*MAX\(\s*1\s*,\s*(\d+)")]
    private static partial Regex MoveGain();

    [GeneratedRegex(@"\b(str|con|dex|intel|wis|cha)\s*=\s*0\s*;")]
    private static partial Regex ZeroedStat();

    [GeneratedRegex(@"case\s+'\w'\s*:\s*GET_RACE\(d->character\)\s*=\s*(RACE_\w+)")]
    private static partial Regex RaceMenuCase();
}
