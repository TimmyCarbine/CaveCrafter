using CaveCrafter.CaveGen.Api;
using Godot;

namespace CaveCrafter.CaveGen.Debug
{
    /// <summary>
    /// Draws cave generator debug overlays.
    /// Phase 1: anchors, endpoints, and intent lines.
    /// </summary>
    public partial class CaveDebugOverlay : Node2D
    {
        [Export]
        public CaveDebugDrawFlags Flags { get; set; } = CaveDebugDrawFlags.Phase1Default;

        /// <summary>
        /// Pixels per tile for drawing. Set this to match world scale.
        /// If tile size is 16px, set to 16. If 32px, set to 32, etc.
        /// </summary>
        [Export(PropertyHint.Range, "1,256,1")]
        public int PixelsPerTile { get; set; } = 32;

        [Export] private NodePath worldGeneratorPath;

        private WorldGenerator _gen;
        private float _worldWidthPx;

        private CaveGenResult _result;

        public override void _Ready()
        {
            _gen = GetNodeOrNull<WorldGenerator>(worldGeneratorPath);
            if (_gen == null)
            {
                GD.PushError("CaveDebugOverlay: worldGeneratorPath is missing / invalid.");
                _worldWidthPx = 0f;
            }
            else
            {
                _worldWidthPx = _gen.WorldWidthTiles * PixelsPerTile;
            }
        }

        public void SetResult(CaveGenResult result)
        {
            _result = result;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_result == null || _result.HighwayIntents == null) return;

            foreach (var intent in _result.HighwayIntents)
            {
                Vector2 start = ToWorld(intent.Start.X, intent.Start.Y);
                Vector2 end = ToWorld(intent.End.X, intent.End.Y);

                if (_worldWidthPx > 0f)
                {
                    float wrappedEndX = WrapMath.NearestImageX(end.X, start.X, _worldWidthPx);
                    end = new Vector2(wrappedEndX, end.Y);
                }

                if (Flags.HasFlag(CaveDebugDrawFlags.IntentLines))
                {
                    DrawLine(start, end, Colors.Red, 6.0f);
                }

                if (Flags.HasFlag(CaveDebugDrawFlags.Anchors))
                {
                    DrawCircle(start, 12.0f, Colors.Orange);
                }

                if (Flags.HasFlag(CaveDebugDrawFlags.Endpoints))
                {
                    DrawCircle(end, 12.0f, Colors.Orange);
                }

                if (Flags.HasFlag(CaveDebugDrawFlags.Labels))
                {
                    DrawString(
                        ThemeDB.FallbackFont,
                        start + new Vector2(24, -24),
                        $"H{intent.HighwayId}",
                        HorizontalAlignment.Left,
                        -1,
                        64,
                        Colors.White
                    );

                    DrawString(
                        ThemeDB.FallbackFont,
                        end + new Vector2(-80, 80),
                        $"H{intent.HighwayId}",
                        HorizontalAlignment.Left,
                        -1,
                        64,
                        Colors.White
                    );
                }
            }
        }

        private Vector2 ToWorld(float tileX, float tileY)
        {
            return new Vector2(tileX * PixelsPerTile, tileY * PixelsPerTile);
        }
    }
}