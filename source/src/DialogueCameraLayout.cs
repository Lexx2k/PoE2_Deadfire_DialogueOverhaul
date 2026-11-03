using System;

namespace Deadfire.DialogOverhaul
{
    public static class DialogueCameraLayout
    {
        public static float FocusViewportX(float viewportLeft, float viewportWidth, float panelLeft, float speakerBiasPixels = 0)
        {
            if (viewportWidth <= 0) return .5f;
            float available = Math.Max(0, Math.Min(viewportWidth, panelLeft - viewportLeft));
            if (available >= viewportWidth) return .5f;
            return Math.Max(0, Math.Min(available, available * .5f + speakerBiasPixels)) / viewportWidth;
        }
    }
}
