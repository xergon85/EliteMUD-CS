namespace EliteMud.Game;

/// <summary>
/// Runtime instance of an object in the world.
/// Legacy: struct obj_data in structs.h
/// </summary>
public sealed class ObjectInstance
{
    private readonly List<ObjectInstance> _contents = new();

    public ObjectInstance(int instanceId, ObjectDefinition definition)
    {
        InstanceId = instanceId;
        Definition = definition;
        
        // Initialize container state from definition flags
        if (definition.Details?.Container != null)
        {
            var container = definition.Details.Container;
            IsClosed = container.Flags.Contains("Closed", StringComparer.OrdinalIgnoreCase);
            IsLocked = container.Flags.Contains("Locked", StringComparer.OrdinalIgnoreCase);
        }

        if (string.Equals(definition.Type, "Portal", StringComparison.OrdinalIgnoreCase))
        {
            Portal = PortalState.FromValues(definition.Values);
        }
    }

    public int InstanceId { get; }
    public ObjectDefinition Definition { get; }
    
    /// <summary>
    /// Runtime state: Is this container currently closed?
    /// Only applies to containers with "Closeable" flag.
    /// Legacy: GET_OBJ_VAL(obj, 1) & CONT_CLOSED
    /// </summary>
    public bool IsClosed { get; set; }
    
    /// <summary>
    /// Runtime state: Is this container currently locked?
    /// Only applies to containers with "Closeable" flag.
    /// Requires a key (KeyId in container details) to unlock.
    /// Legacy: GET_OBJ_VAL(obj, 1) & CONT_LOCKED
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Runtime state for portal objects; null for every other object type.
    /// </summary>
    public PortalState? Portal { get; }

    /// <summary>
    /// Items contained within this object (for containers like corpses, bags, etc.)
    /// Legacy: obj_data->contains
    /// </summary>
    public IReadOnlyList<ObjectInstance> Contents => _contents;

    /// <summary>
    /// Add an item to this container.
    /// Legacy: obj_to_obj()
    /// </summary>
    public void AddItem(ObjectInstance item)
    {
        _contents.Add(item);
    }

    /// <summary>
    /// Remove an item from this container.
    /// </summary>
    public bool RemoveItem(ObjectInstance item)
    {
        return _contents.Remove(item);
    }

    /// <summary>
    /// Remove an item from this container by instance ID.
    /// </summary>
    public bool RemoveItemById(int instanceId)
    {
        var item = _contents.FirstOrDefault(i => i.InstanceId == instanceId);
        if (item is null)
        {
            return false;
        }
        return _contents.Remove(item);
    }
}
