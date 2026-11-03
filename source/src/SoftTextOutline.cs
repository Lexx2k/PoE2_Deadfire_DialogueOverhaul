using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deadfire.DialogOverhaul
{
    // Two low-opacity radial rings approximate a soft shadow without changing
    // the font material or the original glyphs. Run after HangingIndent.
    public sealed class SoftTextOutline : BaseMeshEffect
    {
        private readonly List<UIVertex> original = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            original.Clear();
            helper.GetUIVertexStream(original);
            if (original.Count == 0) return;
            // Stay within the legacy Canvas mesh limit on unusually long entries.
            int samples = original.Count * 17 < 60000 ? 8 : 4;
            helper.Clear();
            Ring(helper, samples, .95f, .075f, Mathf.PI / samples);
            Ring(helper, samples, .4f, .12f, 0f);
            int start = helper.currentVertCount;
            for (int i = 0; i < original.Count; i++)
            {
                helper.AddVert(original[i]);
                if (i % 3 == 2) helper.AddTriangle(start + i - 2, start + i - 1, start + i);
            }
        }

        private void Ring(VertexHelper helper, int samples, float radius, float opacity, float phase)
        {
            int start = helper.currentVertCount;
            for (int sample = 0; sample < samples; sample++)
            {
                float angle = phase + sample * Mathf.PI * 2f / samples;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                for (int i = 0; i < original.Count; i++)
                {
                    UIVertex vertex = original[i];
                    vertex.position += offset;
                    vertex.color = new Color32(0, 0, 0, (byte)(vertex.color.a * opacity));
                    helper.AddVert(vertex);
                    if (i % 3 == 2) helper.AddTriangle(start + i - 2, start + i - 1, start + i);
                }
                start += original.Count;
            }
        }
    }
}
