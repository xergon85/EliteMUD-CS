#!/usr/bin/env python3
"""
Fix corrupted WearFlags in zone JSON files.

The legacy import has weapons with [Take, Waist] when they should have [Take, Wield].
This script fixes weapons and other item types to have correct wear slots.
"""

import json
import os
import glob


def fix_wear_flags(obj):
    """Fix WearFlags for an object based on its Type."""
    obj_type = obj.get("Type", "")
    wear_flags = obj.get("WearFlags", [])

    # Skip if no wear flags
    if not wear_flags:
        return obj

    # Always keep Take if present
    has_take = "Take" in wear_flags

    # Fix based on type
    if obj_type in ["Weapon", "FireWeapon"]:
        # Weapons: should be [Take, Wield] or [Take, WieldTwoHanded]
        if "Waist" in wear_flags:
            # Replace Waist with Wield
            obj["WearFlags"] = ["Take", "Wield"] if has_take else ["Wield"]
        elif "WristRight" in wear_flags or "WristLeft" in wear_flags:
            # Two-handed weapons mistakenly marked as wrist items
            obj["WearFlags"] = (
                ["Take", "WieldTwoHanded"] if has_take else ["WieldTwoHanded"]
            )

    elif obj_type == "Shield":
        # Shields: should be [Take, Shield]
        if "Hands" in wear_flags:
            obj["WearFlags"] = ["Take", "Shield"] if has_take else ["Shield"]

    elif obj_type == "Light":
        # Light sources: should be [Take, Light] or [Take, Hold]
        if has_take and len(wear_flags) == 1:
            obj["WearFlags"] = ["Take", "Light"]

    return obj


def fix_zone_file(filepath):
    """Fix WearFlags in a single zone file."""
    print(f"Processing {filepath}...")

    with open(filepath, "r", encoding="utf-8") as f:
        data = json.load(f)

    objects = data.get("Objects", [])
    fixed_count = 0

    for obj in objects:
        old_flags = obj.get("WearFlags", []).copy()
        obj = fix_wear_flags(obj)
        new_flags = obj.get("WearFlags", [])

        if old_flags != new_flags:
            fixed_count += 1
            print(f"  Fixed #{obj['Id']}: {old_flags} -> {new_flags}")

    if fixed_count > 0:
        with open(filepath, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)
        print(f"  Saved {fixed_count} fixes to {filepath}")

    return fixed_count


def main():
    """Fix all zone files."""
    zone_files = glob.glob("zones/*.json")
    total_fixed = 0

    for zone_file in sorted(zone_files):
        total_fixed += fix_zone_file(zone_file)

    print(f"\nTotal objects fixed: {total_fixed}")


if __name__ == "__main__":
    main()
