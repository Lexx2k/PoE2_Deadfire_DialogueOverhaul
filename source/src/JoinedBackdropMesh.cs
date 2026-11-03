using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deadfire.DialogOverhaul
{
    // Partition native sprite geometry at one shared seam. No pixel copying,
    // replacement textures or overlapping translucent fragments are required.
    public sealed class JoinedBackdropMesh : MonoBehaviour, IMeshModifier
    {
        private RectTransform column, portrait;
        private Graphic target;
        private bool isPortrait, lastVisible;
        private Rect lastRect;
        private Vector2 lastPosition;
        private Matrix4x4 outputTransform;
        private readonly List<UIVertex> input = new List<UIVertex>();
        public void Init(RectTransform panel, RectTransform frame, bool portraitLayer)
        {
            column = panel; portrait = frame; isPortrait = portraitLayer;
            target = GetComponent<Graphic>(); target.SetVerticesDirty();
        }
        private void LateUpdate()
        {
            if (column == null || portrait == null || target == null) return;
            bool visible = portrait.gameObject.activeInHierarchy;
            if (visible != lastVisible || portrait.rect != lastRect || portrait.anchoredPosition != lastPosition)
            {
                lastVisible = visible; lastRect = portrait.rect; lastPosition = portrait.anchoredPosition;
                target.SetVerticesDirty();
            }
        }
        public void ModifyMesh(VertexHelper helper)
        {
            if (!enabled || column == null || portrait == null || !portrait.gameObject.activeInHierarchy) return;
            // The seam lies underneath the overlapping artwork, beyond the
            // column sprite's ragged left edge and inside the portrait frame.
            float seam = column.rect.xMin + 14f;
            Vector3[] corners = new Vector3[4]; portrait.GetWorldCorners(corners);
            float bottom = column.InverseTransformPoint(corners[0]).y;
            float top = column.InverseTransformPoint(corners[2]).y;
            Matrix4x4 toColumn = isPortrait ? column.worldToLocalMatrix * portrait.localToWorldMatrix : Matrix4x4.identity;
            outputTransform = isPortrait ? portrait.worldToLocalMatrix * column.localToWorldMatrix : Matrix4x4.identity;
            float transition = 16f, slope = 14f / transition;
            input.Clear(); helper.GetUIVertexStream(input); helper.Clear();
            if (isPortrait) for (int i = 0; i < input.Count; i++) { UIVertex vertex = input[i]; vertex.position = toColumn.MultiplyPoint3x4(vertex.position); input[i] = vertex; }
            for (int i = 0; i + 2 < input.Count; i += 3)
            {
                var triangle = new List<UIVertex> { input[i], input[i + 1], input[i + 2] };
                if (!isPortrait)
                {
                    Emit(helper, Clip(triangle, 1, top, true));
                    Emit(helper, Clip(triangle, 1, bottom, false));
                }
                var middle = Clip(triangle, 1, bottom + transition, true);
                if (!isPortrait) middle = Clip(middle, 1, top, false);
                Emit(helper, Clip(middle, 0, seam, !isPortrait));
                var taper = Clip(Clip(triangle, 1, bottom, true), 1, bottom + transition, false);
                Emit(helper, ClipLine(taper, 1, -slope, column.rect.xMin - slope * bottom, !isPortrait));
            }
        }
        private static List<UIVertex> Clip(List<UIVertex> polygon, int axis, float bound, bool greater)
        { return ClipLine(polygon, axis == 0 ? 1 : 0, axis == 1 ? 1 : 0, bound, greater); }
        private static List<UIVertex> ClipLine(List<UIVertex> polygon, float x, float y, float bound, bool greater)
        {
            var output = new List<UIVertex>();
            if (polygon.Count == 0) return output;
            UIVertex previous = polygon[polygon.Count - 1];
            float prior = previous.position.x * x + previous.position.y * y;
            bool previousInside = greater ? prior >= bound : prior <= bound;
            foreach (UIVertex current in polygon)
            {
                float coordinate = current.position.x * x + current.position.y * y;
                bool inside = greater ? coordinate >= bound : coordinate <= bound;
                if (inside != previousInside)
                {
                    float t = (bound - prior) / (coordinate - prior);
                    UIVertex vertex = previous;
                    vertex.position = previous.position + (current.position - previous.position) * t;
                    vertex.uv0 = previous.uv0 + (current.uv0 - previous.uv0) * t;
                    vertex.uv1 = previous.uv1 + (current.uv1 - previous.uv1) * t;
                    vertex.color = Color32.Lerp(previous.color, current.color, t);
                    output.Add(vertex);
                }
                if (inside) output.Add(current);
                previous = current; prior = coordinate; previousInside = inside;
            }
            return output;
        }
        private void Emit(VertexHelper helper, List<UIVertex> polygon)
        {
            if (polygon.Count < 3) return;
            int first = helper.currentVertCount;
            foreach (UIVertex original in polygon) { UIVertex vertex = original; vertex.position = outputTransform.MultiplyPoint3x4(vertex.position); helper.AddVert(vertex); }
            for (int i = 1; i + 1 < polygon.Count; i++) helper.AddTriangle(first, first + i, first + i + 1);
        }
        public void ModifyMesh(Mesh mesh)
        {
            using (VertexHelper helper = new VertexHelper(mesh)) { ModifyMesh(helper); helper.FillMesh(mesh); }
        }
    }
}
