using UnityEngine;

namespace Deadfire.DialogOverhaul
{
    internal static class PortraitFilter
    {
        // A small unsharp pass on the temporary display copy only. Alpha-weighted
        // neighbours keep transparent edges from producing dark fringes.
        public static void Sharpen(Color32[] pixels, int width, int height)
        {
            Color32[] original = (Color32[])pixels.Clone();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color32 center = original[index];
                    if (center.a == 0) continue;
                    float r = 0, g = 0, b = 0, total = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            Color32 neighbour = original[Mathf.Clamp(y + dy, 0, height - 1) * width + Mathf.Clamp(x + dx, 0, width - 1)];
                            float weight = (dx == 0 ? 2 : 1) * (dy == 0 ? 2 : 1) * neighbour.a;
                            r += neighbour.r * weight; g += neighbour.g * weight; b += neighbour.b * weight; total += weight;
                        }
                    if (total == 0) continue;
                    pixels[index] = new Color32(Channel(center.r, r / total), Channel(center.g, g / total), Channel(center.b, b / total), center.a);
                }
        }
        private static byte Channel(byte original, float blurred)
        {
            float correction = Mathf.Clamp((original - blurred) * .25f, -8f, 8f);
            return (byte)Mathf.Clamp(Mathf.RoundToInt(original + correction), 0, 255);
        }
    }
}
