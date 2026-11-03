using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deadfire.DialogOverhaul
{
    public sealed class TranscriptFade : BaseMeshEffect
    {
        public RectTransform Viewport;
        private const float Distance = 60f;
        private readonly List<UIVertex> vertices = new List<UIVertex>();
        private float lastY = float.NaN, lastScale, lastTop;
        private int lastState = -1;
        private void LateUpdate()
        {
            if (Viewport == null || graphic == null) return;
            Matrix4x4 matrix = Viewport.worldToLocalMatrix * transform.localToWorldMatrix;
            float origin = matrix.m13, top = Viewport.rect.yMax;
            bool changed = float.IsNaN(lastY) || Mathf.Abs(origin - lastY) > .05f || Mathf.Abs(matrix.m11 - lastScale) > .001f || top != lastTop;
            if (!changed) return;
            int state = State(matrix, top);
            if (state != lastState || state == 1) graphic.SetVerticesDirty();
            lastY = origin; lastScale = matrix.m11; lastTop = top; lastState = state;
        }
        private int State(Matrix4x4 matrix, float top)
        {
            Rect rect = ((RectTransform)transform).rect;
            float upper = matrix.MultiplyPoint3x4(new Vector3(0, rect.yMax, 0)).y;
            float lower = matrix.MultiplyPoint3x4(new Vector3(0, rect.yMin, 0)).y;
            if (upper <= top - Distance) return 2;
            if (lower >= top) return 0;
            return 1;
        }
        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || Viewport == null) return;
            Matrix4x4 matrix = Viewport.worldToLocalMatrix * transform.localToWorldMatrix;
            float top = Viewport.rect.yMax;
            if (State(matrix, top) == 2) return;
            vertices.Clear(); helper.GetUIVertexStream(vertices);
            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex vertex = vertices[i];
                float alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((top - matrix.MultiplyPoint3x4(vertex.position).y) / Distance));
                vertex.color.a = (byte)(vertex.color.a * alpha); vertices[i] = vertex;
            }
            helper.Clear(); helper.AddUIVertexTriangleStream(vertices);
        }
    }
}
