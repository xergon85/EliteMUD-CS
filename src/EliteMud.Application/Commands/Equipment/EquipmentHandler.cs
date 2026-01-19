using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Equipment;

public sealed record EquipmentResult(IReadOnlyList<string> Lines);

public sealed class EquipmentHandler
{
    private readonly IWorldState _worldState;

    public EquipmentHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public EquipmentResult Handle(PlayerState player)
    {
        var equipment = _worldState.GetPlayerEquipment(player);
        
        if (equipment.Count == 0)
        {
            return new EquipmentResult(new List<string> { "You are not wearing or wielding anything." });
        }

        var lines = new List<string> { "You are using:" };
        
        // Display equipment in slot order
        var slots = new[]
        {
            (EquipmentSlot.Light, "Light"),
            (EquipmentSlot.FingerRight, "Right Finger"),
            (EquipmentSlot.FingerLeft, "Left Finger"),
            (EquipmentSlot.Neck1, "Neck (1)"),
            (EquipmentSlot.Neck2, "Neck (2)"),
            (EquipmentSlot.Body, "Body"),
            (EquipmentSlot.Head, "Head"),
            (EquipmentSlot.Legs, "Legs"),
            (EquipmentSlot.Feet, "Feet"),
            (EquipmentSlot.Hands, "Hands"),
            (EquipmentSlot.Arms, "Arms"),
            (EquipmentSlot.Shield, "Shield"),
            (EquipmentSlot.About, "About Body"),
            (EquipmentSlot.Waist, "Waist"),
            (EquipmentSlot.WristRight, "Right Wrist"),
            (EquipmentSlot.WristLeft, "Left Wrist"),
            (EquipmentSlot.Wield, "Wielded"),
            (EquipmentSlot.Hold, "Held"),
            (EquipmentSlot.Wield2, "Wielded (2)"),
            (EquipmentSlot.BothHands, "Both Hands")
        };

        foreach (var (slot, slotName) in slots)
        {
            if (equipment.TryGetValue(slot, out var obj))
            {
                lines.Add($"  <{slotName,-15}> {obj.Definition.ShortDescription}");
            }
        }

        return new EquipmentResult(lines);
    }
}
