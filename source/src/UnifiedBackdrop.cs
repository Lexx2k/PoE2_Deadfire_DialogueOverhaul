using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Deadfire.DialogOverhaul
{
    // Composite the union of both silhouettes once. Max alpha avoids applying
    // the same translucent backing twice at the portrait/column join.
    public sealed class UnifiedBackdrop : MonoBehaviour
    {
        private sealed class Pixels { public Color32[] Data; public int Width, Height; }
        private sealed class Layer { public Pixels Pixels; public Rect Source; public Vector4 Border; public Color Tint; public float Ppu; }
        private sealed class Raster { public Rect Column, Frame, Union; public int Width, Height; public Layer A, B; public Color32[] Data; public Exception Error; public long Milliseconds; }
        private static readonly Dictionary<string, Pixels> cache = new Dictionary<string, Pixels>();
        private volatile Raster completed;
        private volatile bool working;
        private Image column, frame;
        private RawImage merged;
        private Texture2D texture;
        private Rect lastColumn, lastFrame;
        private bool ready, failed;
        public void Init(Image background, Image portraitFrame)
        {
            column = background; frame = portraitFrame;
            GameObject go = new GameObject("Joined dialogue backing", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(column.transform, false); go.transform.SetAsFirstSibling();
            merged = go.GetComponent<RawImage>(); merged.raycastTarget = false; merged.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (failed || column == null || frame == null || merged == null) return;
            bool visible = frame.gameObject.activeInHierarchy;
            if (!visible) { column.enabled = true; merged.gameObject.SetActive(false); return; }
            try
            {
                RectTransform root = column.rectTransform;
                Vector3[] corners = new Vector3[4]; frame.rectTransform.GetWorldCorners(corners);
                Vector2 min = root.InverseTransformPoint(corners[0]), max = root.InverseTransformPoint(corners[2]);
                Rect frameRect = new Rect(min, max - min), columnRect = root.rect;
                Raster result = completed;
                if (result != null)
                {
                    completed = null;
                    if (result.Error != null) throw result.Error;
                    if (!Changed(result.Frame, frameRect) && !Changed(result.Column, columnRect))
                    {
                        Upload(result); lastFrame = frameRect; lastColumn = columnRect; ready = true;
                        Debug.Log("[DeadfireDialog] Backing worker raster: " + result.Milliseconds + "ms (off main thread).");
                    }
                }
                bool valid = ready && !Changed(frameRect, lastFrame) && !Changed(columnRect, lastColumn);
                if (!valid && !working) Queue(columnRect, frameRect);
                column.enabled = !valid; frame.enabled = false; merged.gameObject.SetActive(valid);
            }
            catch (Exception ex)
            {
                failed = true; column.enabled = true; frame.enabled = true; merged.gameObject.SetActive(false);
                Debug.LogWarning("[DeadfireDialog] Joined backing unavailable: " + ex.Message);
            }
        }
        private static bool Changed(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) > .1f || Mathf.Abs(a.y - b.y) > .1f || Mathf.Abs(a.width - b.width) > .1f || Mathf.Abs(a.height - b.height) > .1f;
        }
        private void Queue(Rect a, Rect b)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Rect union = Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
            int width = Mathf.CeilToInt(union.width), height = Mathf.CeilToInt(union.height);
            Raster task = new Raster { Column = a, Frame = b, Union = union, Width = width, Height = height, A = Snapshot(column), B = Snapshot(frame) };
            working = true;
            ThreadPool.QueueUserWorkItem(delegate {
                var rasterTime = System.Diagnostics.Stopwatch.StartNew();
                try { Render(task); } catch (Exception ex) { task.Error = ex; }
                task.Milliseconds = rasterTime.ElapsedMilliseconds; completed = task; working = false;
            });
            Debug.Log("[DeadfireDialog] Backing main-thread preparation: " + timer.ElapsedMilliseconds + "ms.");
        }
        private static Layer Snapshot(Image image)
        {
            Sprite sprite = image.sprite; Pixels pixels = Read(sprite);
            return new Layer { Pixels = pixels, Source = new Rect(0, 0, pixels.Width, pixels.Height), Border = sprite.border, Tint = image.color, Ppu = image.pixelsPerUnit };
        }
        private static void Render(Raster task)
        {
            Color32[] output = new Color32[task.Width * task.Height]; Rect union = task.Union;
            for (int y = 0; y < task.Height; y++)
                for (int x = 0; x < task.Width; x++)
                {
                    Vector2 point = new Vector2(union.xMin + (x + .5f) * union.width / task.Width, union.yMin + (y + .5f) * union.height / task.Height);
                    Color one = Sample(task.A, task.Column, point), two = Sample(task.B, task.Frame, point);
                    // Both use the same native material. Blend colour smoothly,
                    // but use the stronger alpha instead of source-over alpha.
                    float sum = one.a + two.a;
                    Color joined = sum > 0 ? (one * one.a + two * two.a) / sum : Color.clear;
                    joined.a = Mathf.Max(one.a, two.a); output[y * task.Width + x] = joined;
                }
            task.Data = output;
        }
        private void Upload(Raster task)
        {
            if (texture != null) Destroy(texture);
            texture = new Texture2D(task.Width, task.Height, TextureFormat.RGBA32, false); texture.SetPixels32(task.Data); texture.Apply(); texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
            merged.texture = texture;
            RectTransform rect = merged.rectTransform; rect.anchorMin = rect.anchorMax = column.rectTransform.pivot; rect.pivot = Vector2.zero;
            rect.anchoredPosition = task.Union.min; rect.sizeDelta = task.Union.size;
        }
        private static Color Sample(Layer layer, Rect target, Vector2 point)
        {
            if (!target.Contains(point)) return Color.clear;
            Pixels pixels = layer.Pixels; Rect source = layer.Source; Vector4 border = layer.Border;
            float ppu = layer.Ppu;
            float x = source.x + Map(point.x - target.x, target.width, source.width, border.x / ppu, border.z / ppu, border.x, border.z) - .5f;
            float y = source.y + Map(point.y - target.y, target.height, source.height, border.y / ppu, border.w / ppu, border.y, border.w) - .5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), (int)source.xMin, (int)source.xMax - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), (int)source.yMin, (int)source.yMax - 1);
            int x1 = Mathf.Min(x0 + 1, (int)source.xMax - 1), y1 = Mathf.Min(y0 + 1, (int)source.yMax - 1);
            Color low = Color.Lerp(pixels.Data[y0 * pixels.Width + x0], pixels.Data[y0 * pixels.Width + x1], x - Mathf.Floor(x));
            Color high = Color.Lerp(pixels.Data[y1 * pixels.Width + x0], pixels.Data[y1 * pixels.Width + x1], x - Mathf.Floor(x));
            return Color.Lerp(low, high, y - Mathf.Floor(y)) * layer.Tint;
        }
        private static float Map(float p, float target, float source, float left, float right, float sourceLeft, float sourceRight)
        {
            float shrink = Mathf.Min(1, target / Mathf.Max(.001f, left + right)); left *= shrink; right *= shrink;
            if (p < left && left > 0) return p / left * sourceLeft;
            if (p > target - right && right > 0) return source - (target - p) / right * sourceRight;
            return sourceLeft + (p - left) / Mathf.Max(.001f, target - left - right) * (source - sourceLeft - sourceRight);
        }
        private static Pixels Read(Sprite sprite)
        {
            Texture2D source = sprite.texture; Rect region = sprite.rect;
            string key = source.GetInstanceID() + ":" + region.ToString("F2");
            Pixels result; if (cache.TryGetValue(key, out result)) return result;
            int width = Mathf.RoundToInt(region.width), height = Mathf.RoundToInt(region.height);
            RenderTexture previous = RenderTexture.active;
            RenderTexture buffer = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try { Graphics.Blit(source, buffer); RenderTexture.active = buffer; copy.ReadPixels(region, 0, 0); result = new Pixels { Data = copy.GetPixels32(), Width = width, Height = height }; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(buffer); Destroy(copy); }
            cache[key] = result; return result;
        }
        private void OnDestroy() { if (texture != null) Destroy(texture); }
    }
}
