using System;

namespace Deadfire.DialogOverhaul
{
    public static class TranscriptLayout
    {
        public const float ResponseTopInset = 30;
        // Anchor the first current reply. NPC text grows above this point;
        // lengthy choice lists push the whole block up only when necessary.
        public static float TrailingSpace(float viewportHeight, float responseListHeight)
        {
            return Math.Max(40f, viewportHeight * .28f - responseListHeight);
        }
        public static float ContentHeight(float viewportHeight, float transcriptBottom, float responseListHeight)
        {
            return transcriptBottom + ResponseTopInset + responseListHeight + TrailingSpace(viewportHeight, responseListHeight);
        }
    }
}
