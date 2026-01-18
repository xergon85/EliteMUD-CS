using System;
using System.Collections.Generic;

namespace EliteMud.Legacy.Import;

internal static class LegacyObjectImporter
{
    public static List<ObjectContent> Load(string objectsPath, CancellationToken cancellationToken)
    {
        var objects = new List<ObjectContent>();
        foreach (var file in Directory.EnumerateFiles(objectsPath, "*.obj"))
        {
            using var reader = LegacyImportUtilities.CreateReader(file);
            var parser = new LegacyImportParser(reader);
            var iterations = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (iterations++ > LegacyImportConstants.MaxIterations)
                {
                    throw new InvalidOperationException($"Object import exceeded safe iteration limit in {file}.");
                }

                var token = parser.ReadToken();
                if (token is null)
                {
                    break;
                }

                if (token == "$")
                {
                    break;
                }

                if (!token.StartsWith('#'))
                {
                    continue;
                }

                var vnum = LegacyImportLookup.ParseInt(token[1..]);
                if (vnum >= 99999)
                {
                    break;
                }

                var name = parser.ReadTildeString();
                var shortDesc = parser.ReadTildeString();
                var description = parser.ReadTildeString();
                var actionDesc = parser.ReadTildeString();

                var type = parser.ReadNumber();
                var level = parser.ReadNumber();
                var antiClass = parser.ReadNumber();
                var extraFlags = parser.ReadNumber();
                var wearFlags = parser.ReadNumber();
                var values = new List<int>();
                for (var i = 0; i < 6; i++)
                {
                    values.Add(parser.ReadNumber());
                }

                var weight = parser.ReadNumber();
                var cost = parser.ReadNumber();
                var costPerDay = parser.ReadNumber();

                var details = BuildObjectDetails(LegacyImportLookup.ItemTypeFromIndex(type), values);

                var extras = new List<ExtraDescriptionContent>();
                var affects = new List<ObjectAffectContent>();
                var bitvectors = new List<string>();
                string? specialProc = null;

                ParseObjectExtensions(parser, extras, affects, bitvectors, ref specialProc);

                var typeName = LegacyImportLookup.ItemTypeFromIndex(type);

                objects.Add(new ObjectContent
                {
                    Id = vnum,
                    Name = name,
                    ShortDescription = shortDesc,
                    Description = description,
                    ActionDescription = actionDesc,
                    Type = typeName,
                    Level = level,
                    AntiClass = LegacyImportLookup.AntiClassFromIndex(antiClass),
                    ExtraFlags = LegacyImportLookup.ItemExtraFlags(extraFlags),
                    WearFlags = LegacyImportLookup.ItemWearFlags(wearFlags),
                    Values = values,
                    Details = details,
                    Weight = weight,
                    Cost = cost,
                    CostPerDay = costPerDay,
                    ExtraDescriptions = extras,
                    Affects = affects,
                    Bitvectors = bitvectors,
                    SpecialProc = specialProc
                });
            }
        }

        return objects;
    }

    private static ObjectDetailsContent? BuildObjectDetails(string typeName, IReadOnlyList<int> values)
    {
        return typeName switch
        {
            "Light" => new ObjectDetailsContent
            {
                Light = new ObjectLightContent(values[0], values[1], values[2])
            },
            "Scroll" => new ObjectDetailsContent
            {
                SpellContainer = new ObjectSpellContainerContent(values[0], new[] { values[1], values[2], values[3] })
            },
            "Potion" => new ObjectDetailsContent
            {
                SpellContainer = new ObjectSpellContainerContent(values[0], new[] { values[1], values[2], values[3] })
            },
            "Wand" => new ObjectDetailsContent
            {
                Charges = new ObjectWandStaffContent(values[3], values[0], values[1], values[2])
            },
            "Staff" => new ObjectDetailsContent
            {
                Charges = new ObjectWandStaffContent(values[3], values[0], values[1], values[2])
            },
            "Weapon" => new ObjectDetailsContent
            {
                Weapon = new ObjectWeaponContent(values[1], values[2], values[3], values[5])
            },
            "FireWeapon" => new ObjectDetailsContent
            {
                Weapon = new ObjectWeaponContent(values[1], values[2], values[3], values[5])
            },
            "Missile" => new ObjectDetailsContent
            {
                Missile = new ObjectMissileContent(values[1], values[3])
            },
            "Armor" => new ObjectDetailsContent
            {
                Armor = new ObjectArmorContent(values[0], values[5])
            },
            "Trap" => new ObjectDetailsContent
            {
                Trap = new ObjectTrapContent(values[0], values[1])
            },
            "Container" => new ObjectDetailsContent
            {
                Container = new ObjectContainerContent(
                    values[0],
                    LegacyImportLookup.ContainerFlags(values[1]),
                    values[2],
                    values[3],
                    values[4],
                    values[5])
            },
            "DrinkContainer" => new ObjectDetailsContent
            {
                Drink = new ObjectDrinkContent(values[0], values[1], values[2], values[3] != 0)
            },
            "Fountain" => new ObjectDetailsContent
            {
                Drink = new ObjectDrinkContent(values[0], values[1], values[2], values[3] != 0)
            },
            "Note" => new ObjectDetailsContent
            {
                Note = new ObjectNoteContent(values[0])
            },
            "Key" => new ObjectDetailsContent
            {
                Key = new ObjectKeyContent(values[0], values[4], values[5])
            },
            "Food" => new ObjectDetailsContent
            {
                Food = new ObjectFoodContent(values[0], values[3] != 0)
            },
            "Money" => new ObjectDetailsContent
            {
                Money = new ObjectMoneyContent(values[0])
            },
            "Portal" => new ObjectDetailsContent
            {
                Portal = new ObjectPortalContent(
                    values[0],
                    LegacyImportLookup.PortalFlags(values[1]),
                    values[2],
                    values[3],
                    values[4],
                    values[5])
            },
            _ => null
        };
    }

    private static void ParseObjectExtensions(
        LegacyImportParser parser,
        List<ExtraDescriptionContent> extras,
        List<ObjectAffectContent> affects,
        List<string> bitvectors,
        ref string? specialProc)
    {
        while (true)
        {
            var token = parser.ReadToken();
            if (token is null)
            {
                break;
            }

            if (token.StartsWith('#') || token == "$")
            {
                parser.PushToken(token);
                break;
            }

            if (token == "E")
            {
                var keywords = parser.ReadTildeString();
                var extraDesc = parser.ReadTildeString();
                extras.Add(new ExtraDescriptionContent(LegacyImportUtilities.SplitKeywords(keywords), extraDesc));
                continue;
            }

            if (token == "A")
            {
                var location = parser.ReadNumber();
                var modifier = parser.ReadNumber();
                affects.Add(new ObjectAffectContent(LegacyImportLookup.ApplyFromIndex(location), modifier));
                continue;
            }

            if (token == "B")
            {
                var bitvector = parser.ReadNumber();
                bitvectors.Add(LegacyImportLookup.BitvectorLabel(bitvector));
                continue;
            }

            if (token.StartsWith("P", StringComparison.Ordinal))
            {
                specialProc = token.Length > 1 ? token[1..] : parser.ReadToken();
                if (specialProc is not null)
                {
                    specialProc = specialProc.Trim();
                    if (specialProc.StartsWith("(", StringComparison.Ordinal) && specialProc.EndsWith(")", StringComparison.Ordinal))
                    {
                        specialProc = specialProc[1..^1];
                    }
                }
            }
        }
    }
}
