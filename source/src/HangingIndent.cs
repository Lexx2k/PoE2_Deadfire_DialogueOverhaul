using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deadfire.DialogOverhaul
{
    // The text rectangle reserves an indent before wrapping; outdent line one
    // in the generated mesh so names and option numbers align with the margin.
    public sealed class HangingIndent : MonoBehaviour, IMeshModifier
    {
        public const float Amount = 16f;
        private Text text;
        private readonly List<UIVertex> vertices = new List<UIVertex>();

        private void OnEnable()
        {
            text = GetComponent<Text>();
            if (text != null) text.SetVerticesDirty();
        }

        public void ModifyMesh(VertexHelper helper)
        {
            if (!enabled || text == null || string.IsNullOrEmpty(text.text)) return;
            vertices.Clear();
            helper.GetUIVertexStream(vertices);
            if (vertices.Count < 6) return;
            float highestCenter = float.NegativeInfinity;
            for (int i = 0; i + 5 < vertices.Count; i += 6)
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                for (int j = 0; j < 6; j++) { min = Mathf.Min(min, vertices[i + j].position.y); max = Mathf.Max(max, vertices[i + j].position.y); }
                if (max - min > 0.01f) highestCenter = Mathf.Max(highestCenter, (min + max) * 0.5f);
            }
            float lineHeight = text.fontSize * text.lineSpacing;
            if (text.cachedTextGenerator.lines.Count > 0)
                lineHeight = text.cachedTextGenerator.lines[0].height / Mathf.Max(0.01f, text.pixelsPerUnit);
            float firstLineThreshold = highestCenter - lineHeight * 0.65f;
            for (int i = 0; i + 5 < vertices.Count; i += 6)
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                for (int j = 0; j < 6; j++) { min = Mathf.Min(min, vertices[i + j].position.y); max = Mathf.Max(max, vertices[i + j].position.y); }
                if ((min + max) * 0.5f < firstLineThreshold) continue;
                for (int j = 0; j < 6; j++)
                {
                    UIVertex vertex = vertices[i + j];
                    vertex.position.x -= Amount;
                    vertices[i + j] = vertex;
                }
            }
            helper.Clear();
            helper.AddUIVertexTriangleStream(vertices);
        }

        public void ModifyMesh(Mesh mesh)
        {
            using (VertexHelper helper = new VertexHelper(mesh)) { ModifyMesh(helper); helper.FillMesh(mesh); }
        }
    }
}





