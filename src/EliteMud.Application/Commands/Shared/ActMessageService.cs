using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Shared;

/// <summary>
/// Message targeting flags for room broadcasting.
/// Based on legacy TO_CHAR, TO_VICT, TO_ROOM, TO_NOTVICT flags.
/// </summary>
[Flags]
public enum ActTarget
{
    /// <summary>
    /// Send message to the actor (the person performing the action)
    /// </summary>
    ToChar = 1 << 0,
    
    /// <summary>
    /// Send message to the victim/target of the action
    /// </summary>
    ToVict = 1 << 1,
    
    /// <summary>
    /// Send message to everyone in the room (including actor and victim)
    /// </summary>
    ToRoom = 1 << 2,
    
    /// <summary>
    /// Send message to everyone in the room EXCEPT actor and victim
    /// </summary>
    ToNotVict = 1 << 3
}

/// <summary>
/// Service for formatting messages with substitution codes.
/// The actual sending is done by command handlers using ConnectionContext.
/// 
/// Based on legacy comm.c perform_act() function for message formatting.
/// </summary>
public class ActMessageService
{
    /// <summary>
    /// Format a message with substitution codes for a specific viewer.
    /// Supports PlayerState and MobInstance as actors/victims.
    /// 
    /// Substitution codes:
    /// - $n - actor's name (to others) or "you" (to self)
    /// - $N - victim's name (to others) or "you" (to victim)
    /// - $e - he/she/it (actor)
    /// - $E - he/she/it (victim)
    /// - $m - him/her/it (actor)
    /// - $M - him/her/it (victim)
    /// - $s - his/her/its (actor)
    /// - $S - his/her/its (victim)
    /// - $o - object name
    /// - $p - object short description
    /// - $a - a/an for object
    /// </summary>
    /// <param name="message">Message template with substitution codes</param>
    /// <param name="viewer">The player who will see this message</param>
    /// <param name="actor">The character performing the action (PlayerState or MobInstance)</param>
    /// <param name="victim">The target character (PlayerState or MobInstance, optional)</param>
    /// <param name="obj">The object involved (optional)</param>
    /// <param name="textArg">Additional text argument (optional)</param>
    /// <returns>Formatted message ready to send</returns>
    public string FormatMessage(
        string message,
        PlayerState viewer,
        object? actor = null,
        object? victim = null,
        ObjectDefinition? obj = null,
        string? textArg = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var result = message;

        // Determine if viewer is the actor or victim
        bool viewerIsActor = actor != null && IsViewerEntity(viewer, actor);
        bool viewerIsVictim = victim != null && IsViewerEntity(viewer, victim);

        // Actor substitutions
        if (actor != null)
        {
            result = result.Replace("$n", viewerIsActor ? "you" : GetEntityName(actor));
            result = result.Replace("$e", GetSubjectPronoun(actor, viewerIsActor));
            result = result.Replace("$m", GetObjectPronoun(actor, viewerIsActor));
            result = result.Replace("$s", GetPossessivePronoun(actor, viewerIsActor));
        }

        // Victim substitutions
        if (victim != null)
        {
            result = result.Replace("$N", viewerIsVictim ? "you" : GetEntityName(victim));
            result = result.Replace("$E", GetSubjectPronoun(victim, viewerIsVictim));
            result = result.Replace("$M", GetObjectPronoun(victim, viewerIsVictim));
            result = result.Replace("$S", GetPossessivePronoun(victim, viewerIsVictim));
        }

        // Object substitutions
        if (obj != null)
        {
            result = result.Replace("$o", obj.Name);
            result = result.Replace("$p", obj.ShortDescription);
            result = result.Replace("$a", GetArticle(obj.Name));
        }

        // Text argument substitution
        if (textArg != null)
        {
            result = result.Replace("$t", textArg);
            result = result.Replace("$T", textArg);
        }

        // Capitalize first letter and add line ending
        if (!string.IsNullOrEmpty(result))
        {
            result = char.ToUpper(result[0]) + result.Substring(1);
            if (!result.EndsWith("\r\n"))
                result += "\r\n";
        }

        return result;
    }

    /// <summary>
    /// Check if viewer is the same entity as the given actor/victim
    /// </summary>
    private bool IsViewerEntity(PlayerState viewer, object entity)
    {
        return entity switch
        {
            PlayerState player => viewer.Id == player.Id,
            MobInstance => false, // Viewer is never a mob
            _ => false
        };
    }

    /// <summary>
    /// Get the name/description of an entity (player or mob)
    /// </summary>
    private string GetEntityName(object entity)
    {
        return entity switch
        {
            PlayerState player => player.Name,
            MobInstance mob => mob.Definition.ShortDescription,
            _ => "someone"
        };
    }

    /// <summary>
    /// Get the sex of an entity (player or mob)
    /// </summary>
    private byte GetEntitySex(object entity)
    {
        return entity switch
        {
            PlayerState player => player.Sex,
            MobInstance mob => 0, // TODO: Extract sex from mob flags or add Sex field to MobDefinition
            _ => 0 // Neutral
        };
    }

    /// <summary>
    /// Get subject pronoun (he/she/it) based on character sex.
    /// Legacy: HSSH macro
    /// </summary>
    private string GetSubjectPronoun(object entity, bool toSelf)
    {
        if (toSelf) return "you";

        var sex = GetEntitySex(entity);
        return sex switch
        {
            1 => "he",      // Male
            2 => "she",     // Female
            _ => "it"       // Neutral
        };
    }

    /// <summary>
    /// Get object pronoun (him/her/it) based on character sex.
    /// Legacy: HMHR macro
    /// </summary>
    private string GetObjectPronoun(object entity, bool toSelf)
    {
        if (toSelf) return "you";

        var sex = GetEntitySex(entity);
        return sex switch
        {
            1 => "him",     // Male
            2 => "her",     // Female
            _ => "it"       // Neutral
        };
    }

    /// <summary>
    /// Get possessive pronoun (his/her/its) based on character sex.
    /// Legacy: HSHR macro
    /// </summary>
    private string GetPossessivePronoun(object entity, bool toSelf)
    {
        if (toSelf) return "your";

        var sex = GetEntitySex(entity);
        return sex switch
        {
            1 => "his",     // Male
            2 => "her",     // Female
            _ => "its"      // Neutral
        };
    }

    /// <summary>
    /// Get indefinite article (a/an) for a word.
    /// Legacy: SANA macro
    /// </summary>
    private string GetArticle(string word)
    {
        if (string.IsNullOrEmpty(word))
            return "a";

        var firstChar = char.ToLower(word[0]);
        return "aeiou".Contains(firstChar) ? "an" : "a";
    }
}
