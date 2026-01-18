using System;
using System.Collections.Generic;
using EliteMud.Game;

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

    private static ObjectDetails? BuildObjectDetails(string typeName, IReadOnlyList<int> values)
    {
        return typeName switch
        {
            "Light" => new ObjectDetails
            {
                Light = new ObjectLight(values[0], values[1], values[2])
            },
            "Scroll" => new ObjectDetails
            {
                SpellContainer = new ObjectSpellContainer(values[0], new[] { values[1], values[2], values[3] })
            },
            "Potion" => new ObjectDetails
            {
                SpellContainer = new ObjectSpellContainer(values[0], new[] { values[1], values[2], values[3] })
            },
            "Wand" => new ObjectDetails
            {
                Charges = new ObjectWandStaff(values[3], values[0], values[1], values[2])
            },
            "Staff" => new ObjectDetails
            {
                Charges = new ObjectWandStaff(values[3], values[0], values[1], values[2])
            },
            "Weapon" => new ObjectDetails
            {
                Weapon = new ObjectWeapon(values[1], values[2], values[3], values[5])
            },
            "FireWeapon" => new ObjectDetails
            {
                Weapon = new ObjectWeapon(values[1], values[2], values[3], values[5])
            },
            "Missile" => new ObjectDetails
            {
                Missile = new ObjectMissile(values[1], values[3])
            },
            "Armor" => new ObjectDetails
            {
                Armor = new ObjectArmor(values[0], values[5])
            },
            "Trap" => new ObjectDetails
            {
                Trap = new ObjectTrap(values[0], values[1])
            },
            "Container" => new ObjectDetails
            {
                Container = new ObjectContainer(
                    values[0],
                    LegacyImportLookup.ContainerFlags(values[1]),
                    values[2],
                    values[3],
                    values[4],
                    values[5])
            },
            "DrinkContainer" => new ObjectDetails
            {
                Drink = new ObjectDrink(values[0], values[1], values[2], values[3] != 0)
            },
            "Fountain" => new ObjectDetails
            {
                Drink = new ObjectDrink(values[0], values[1], values[2], values[3] != 0)
            },
            "Note" => new ObjectDetails
            {
                Note = new ObjectNote(values[0])
            },
            "Key" => new ObjectDetails
            {
                Key = new ObjectKey(values[0], values[4], values[5])
            },
            "Food" => new ObjectDetails
            {
                Food = new ObjectFood(values[0], values[3] != 0)
            },
            "Money" => new ObjectDetails
            {
                Money = new ObjectMoney(values[0])
            },
            "Portal" => new ObjectDetails
            {
                Portal = new ObjectPortal(
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
