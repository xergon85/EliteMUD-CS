# World Content Schema (v1)

This schema stores game content as versioned files so Git can track changes. The runtime loads these files to build the world state. Player/runtime data remains in SQLite.

## Directory Layout
```
content/
  rooms/
    rooms.json
  mobs/
    mobs.json
  objects/
    objects.json
  zones/
    zones.json
  scripts/
    scripts.json
  meta/
    schema.json
```

## Common Conventions
- All entities include a numeric `id` that matches legacy vnums where applicable.
- All files use UTF-8 encoded JSON.
- `schema.json` declares schema version and optional build metadata.

## Schema Metadata
**File:** `content/meta/schema.json`
```json
{
  "version": 1,
  "createdUtc": "2026-01-17T00:00:00Z",
  "notes": "Initial schema for rooms/mobs/objects/zones/scripts."
}
```

## Rooms
**File:** `content/rooms/rooms.json`
```json
{
  "rooms": [
    {
      "id": 1,
      "name": "The Entry Hall",
      "description": "A simple stone hall with torchlight flickering along the walls.",
      "sector": "Inside",
      "flags": ["Indoors"],
      "exits": [
        { "direction": "North", "targetId": 2, "flags": ["IsDoor"], "keyId": null }
      ],
      "extraDescriptions": [
        { "keywords": ["torch", "light"], "description": "The torches burn steadily." }
      ]
    }
  ]
}
```

## Mobs (NPCs)
**File:** `content/mobs/mobs.json`
```json
{
  "mobs": [
    {
      "id": 1001,
      "name": "training dummy",
      "shortDescription": "a worn training dummy",
      "longDescription": "A worn training dummy stands here.",
      "description": "Stuffed with straw and stitched roughly.",
      "level": 1,
      "race": "Human",
      "class": "Warrior",
      "flags": ["Sentinel"],
      "stats": { "strength": 10, "dexterity": 10, "intelligence": 10, "wisdom": 10, "constitution": 10, "charisma": 10 },
      "resistances": [],
      "skills": []
    }
  ]
}
```

## Objects
**File:** `content/objects/objects.json`
```json
{
  "objects": [
    {
      "id": 2001,
      "name": "practice sword",
      "shortDescription": "a practice sword",
      "longDescription": "A practice sword rests here.",
      "description": "Blunted steel with a wrapped grip.",
      "type": "Weapon",
      "wearSlots": ["Wield"],
      "flags": ["NoDrop"],
      "values": { "damageDice": "1d4", "damageType": "Slash" },
      "weight": 3,
      "cost": 10
    }
  ]
}
```

## Zones (Resets/Spawns)
**File:** `content/zones/zones.json`
```json
{
  "zones": [
    {
      "id": 10,
      "name": "Training Grounds",
      "roomRange": { "min": 1, "max": 99 },
      "resetMode": "ResetAlways",
      "resetCommands": [
        { "type": "LoadMob", "mobId": 1001, "roomId": 2, "maxExisting": 1 }
      ]
    }
  ]
}
```

## Scripts
**File:** `content/scripts/scripts.json`
```json
{
  "scripts": [
    {
      "id": "entry-hall-look",
      "hook": "OnLook",
      "when": { "roomId": 1 },
      "body": "emit('Dust motes drift lazily in the torchlight.')"
    }
  ]
}
```

## Future Extensions
- Split large files into sharded chunks per zone/area.
- Add localization dictionaries for multi-language text.
- Attach binary assets via `assets/` with manifest references.
