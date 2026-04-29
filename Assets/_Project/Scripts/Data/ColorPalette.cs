using UnityEngine;

namespace DragonRescue.Data
{
    /// <summary>
    /// Single source of truth for CannonColor → UnityEngine.Color mapping.
    /// Eliminates duplicated GetColorFromEnum across multiple scripts.
    /// </summary>
    public static class ColorPalette
    {
        public static Color GetColor(CannonColor cannonColor)
        {
            return cannonColor switch
            {
                CannonColor.Blue   => new Color(0.2f, 0.4f, 1f),
                CannonColor.Green  => new Color(0.2f, 0.8f, 0.3f),
                CannonColor.Red    => new Color(1f, 0.25f, 0.25f),
                CannonColor.Yellow => new Color(1f, 0.85f, 0.15f),
                CannonColor.Purple => new Color(0.6f, 0.2f, 0.8f),
                CannonColor.Pink   => new Color(1f, 0.4f, 0.7f),
                CannonColor.Cyan   => new Color(0.2f, 0.9f, 0.9f),
                CannonColor.Brown  => new Color(0.6f, 0.35f, 0.15f),
                _ => Color.white
            };
        }
    }
}
