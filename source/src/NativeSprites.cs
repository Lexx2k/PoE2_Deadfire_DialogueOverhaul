using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Deadfire.DialogOverhaul
{
    // Keep the shipped atlas artwork (including its alpha edges and borders).
    public static class NativeSprites
    {
        public static Sprite Copy(Component source)
        {
            try { return CopyUnsafe(source); }
            catch (Exception ex) { Debug.LogWarning("[DeadfireDialog] Native sprite unavailable: " + ex.Message); return null; }
        }
        private static Sprite CopyUnsafe(Component source)
        {
            if (source == null) return null;
            foreach (MethodInfo method in source.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (method.Name == "UpdateUVs" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(bool)) { method.Invoke(source, new object[] { true }); break; }
            object atlas = DialogueView.Read(source, "atlas");
            Texture2D texture = DialogueView.Read(atlas, "texture") as Texture2D;
            object outerValue = DialogueView.Read(source, "outerUV");
            if (texture == null || !(outerValue is Rect)) return null;
            Rect uv = (Rect)outerValue;
            if (uv.width <= 0 || uv.height <= 0) return null;
            Rect pixels = new Rect(uv.x * texture.width, uv.y * texture.height, uv.width * texture.width, uv.height * texture.height);
            object innerValue = DialogueView.Read(source, "innerUV");
            Vector4 border = Vector4.zero;
            if (innerValue is Rect)
            {
                Rect inner = (Rect)innerValue;
                border = new Vector4(Mathf.Clamp((inner.xMin - uv.xMin) * texture.width, 0, pixels.width * .49f), Mathf.Clamp((inner.yMin - uv.yMin) * texture.height, 0, pixels.height * .49f), Mathf.Clamp((uv.xMax - inner.xMax) * texture.width, 0, pixels.width * .49f), Mathf.Clamp((uv.yMax - inner.yMax) * texture.height, 0, pixels.height * .49f));
            }
            Sprite result = Sprite.Create(texture, pixels, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            result.name = "Native " + Convert.ToString(DialogueView.Read(source, "spriteName"));
            return result;
        }
        public static bool Apply(Image target, Component source)
        {
            Sprite sprite = Copy(source); if (sprite == null) return false;
            target.sprite = sprite; target.type = Image.Type.Sliced;
            object nativeColor = DialogueView.Read(source, "color");
            Color tint = nativeColor is Color ? (Color)nativeColor : Color.white; tint.a = 1; target.color = tint;
            Debug.Log("[DeadfireDialog] Reused sprite " + sprite.name);
            return true;
        }
    }
}
