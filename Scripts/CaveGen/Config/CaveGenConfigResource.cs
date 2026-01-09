using Godot;

namespace CaveCrafter.CaveGen.Config
{
    /// <summary>
    /// Top level cave generation configuration asset.
    /// Holds references to per-tunnel-class resources.
    /// </summary>
    [GlobalClass]
    public partial class CaveGenConfigResource : Resource
    {
        [Export]
        public TunnelClassConfigResource Highway { get; set; } = new TunnelClassConfigResource();

        [Export]
        public TunnelClassConfigResource Connector { get; set; } = new TunnelClassConfigResource();

        [Export]
        public TunnelClassConfigResource Link { get; set; } = new TunnelClassConfigResource();
    }
}