namespace AncientOrder.Stage;

/// <summary>
/// The 3D physics layers the town uses for picking, as masks (layer 1 is bit 1, layer 2 is bit 2, layer 3 is bit 4).
/// They match the layer names in project.godot.
/// </summary>
public static class PickLayers
{
    public const uint Ground = 1u << 0;
    public const uint People = 1u << 1;
    public const uint Spots = 1u << 2;
}
