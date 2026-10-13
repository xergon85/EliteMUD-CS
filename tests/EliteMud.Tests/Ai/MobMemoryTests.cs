using EliteMud.Application.Ai;
using EliteMud.Application.Progression;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Ai;

/// <summary>
/// Who a MOB_MEMORY mob remembers (legacy damage(), fight.c:824 and :1065).
/// </summary>
public class MobMemoryTests
{
    private readonly MobMemory _memory = new();
    private readonly PlayerState _player = new(1, "Tester", 3001, level: 10, "warrior", "human", sex: 1);

    [Fact]
    public void AMemoryMob_RemembersAMortalWhoHurtsIt()
    {
        var mob = Mob(MobFlags.Memory);

        _memory.Hurt(_player, mob);

        Assert.Contains(_player.Id, mob.Memory);
    }

    [Fact]
    public void AMobWithoutMemory_RemembersNobody()
    {
        var mob = Mob(MobFlags.None);

        _memory.Hurt(_player, mob);

        Assert.Empty(mob.Memory);
    }

    [Fact]
    public void AnImmortal_IsNotRemembered()
    {
        var mob = Mob(MobFlags.Memory);
        _player.Level = ExperienceService.MaxMortalLevel + 1;

        _memory.Hurt(_player, mob);

        Assert.Empty(mob.Memory);
    }

    [Fact]
    public void AMemoryMob_ForgetsAPlayerItKills()
    {
        var mob = Mob(MobFlags.Memory);
        mob.RememberPlayer(_player.Id);

        _memory.Killed(mob, _player);

        Assert.Empty(mob.Memory);
    }

    private static MobInstance Mob(MobFlags flags)
    {
        var definition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);
        return new MobInstance(1, definition with { ParsedFlags = flags });
    }
}
