using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EliteMud.Game;
using MoonSharp.Interpreter;

namespace EliteMud.Scripting;

public enum ScriptHook
{
    OnEnterRoom,
    OnSay,
    OnLook
}

public sealed class ScriptContext
{
    private readonly List<string> _outputs = new();

    public ScriptContext(PlayerState player, RoomDefinition room, string? text)
    {
        Player = player;
        Room = room;
        Text = text;
    }

    public PlayerState Player { get; }

    public RoomDefinition Room { get; }

    public string? Text { get; }

    public IReadOnlyList<string> Outputs => _outputs;

    public void AddOutput(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _outputs.Add(message);
        }
    }
}

public interface IScriptEngine
{
    ValueTask ExecuteAsync(ScriptHook hook, ScriptContext context, CancellationToken cancellationToken);
}

public interface IScriptRegistry
{
    ValueTask RegisterAsync(ScriptDefinition script, CancellationToken cancellationToken);
}

public sealed class LuaScriptEngine : IScriptEngine, IScriptRegistry
{
    private readonly Dictionary<ScriptHook, List<ScriptDefinition>> _scripts = new();

    public ValueTask RegisterAsync(ScriptDefinition script, CancellationToken cancellationToken)
    {
        var hook = ParseHook(script.Hook);
        if (!_scripts.TryGetValue(hook, out var list))
        {
            list = new List<ScriptDefinition>();
            _scripts[hook] = list;
        }

        list.Add(script);
        return ValueTask.CompletedTask;
    }

    public ValueTask ExecuteAsync(ScriptHook hook, ScriptContext context, CancellationToken cancellationToken)
    {
        if (!_scripts.TryGetValue(hook, out var scripts))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var scriptDefinition in scripts)
        {
            if (scriptDefinition.RoomId.HasValue && scriptDefinition.RoomId != context.Room.Id)
            {
                continue;
            }

            ExecuteScript(scriptDefinition, context);
        }

        return ValueTask.CompletedTask;
    }

    private static void ExecuteScript(ScriptDefinition scriptDefinition, ScriptContext context)
    {
        var script = new Script();
        script.Globals["player"] = BuildPlayerTable(script, context.Player);
        script.Globals["room"] = BuildRoomTable(script, context.Room);
        script.Globals["text"] = context.Text ?? string.Empty;
        script.Globals["emit"] = (Action<string>)context.AddOutput;

        try
        {
            script.DoString(scriptDefinition.Body);
        }
        catch (ScriptRuntimeException exception)
        {
            context.AddOutput($"[Script Error] {exception.DecoratedMessage}");
        }
    }

    private static Table BuildPlayerTable(Script script, PlayerState player)
    {
        var table = new Table(script);
        table["id"] = player.Id;
        table["name"] = player.Name;
        table["roomId"] = player.RoomId;
        return table;
    }

    private static Table BuildRoomTable(Script script, RoomDefinition room)
    {
        var table = new Table(script);
        table["id"] = room.Id;
        table["name"] = room.Name;
        table["description"] = room.Description;
        return table;
    }

    private static ScriptHook ParseHook(string hook)
    {
        return hook.Trim() switch
        {
            "OnEnterRoom" => ScriptHook.OnEnterRoom,
            "OnSay" => ScriptHook.OnSay,
            "OnLook" => ScriptHook.OnLook,
            _ => throw new ArgumentOutOfRangeException(nameof(hook), hook, "Unknown script hook.")
        };
    }
}
