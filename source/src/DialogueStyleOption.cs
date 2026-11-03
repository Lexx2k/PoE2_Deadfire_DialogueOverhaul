using System;
using System.Reflection;
using UnityEngine;

namespace Deadfire.DialogOverhaul
{
    internal static class DialoguePreferences
    {
        private const string Key = "Deadfire.DialogOverhaul.VerticalInterface";
        public static bool Enabled
        {
            get
            {
                const string legacyKey = "Deadfire.DialogueOverhaul.VerticalInterface";
                if (!PlayerPrefs.HasKey(Key) && PlayerPrefs.HasKey(legacyKey))
                {
                    PlayerPrefs.SetInt(Key, PlayerPrefs.GetInt(legacyKey, 1));
                    PlayerPrefs.Save();
                }
                return PlayerPrefs.GetInt(Key, 1) != 0;
            }
            set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }

    public sealed class DialogueStyleOption : MonoBehaviour
    {
        private Component checkbox;
        private Component toggleButton;
        private bool syncing;
        private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        public static void Attach(Component manager)
        {
            if (manager.GetComponentInChildren<DialogueStyleOption>(true) != null) return;
            Type tagType = Type.GetType("Game.UI.UIOptionsTag, Assembly-CSharp");
            Component selected = null;
            foreach (Component tag in manager.GetComponentsInChildren(tagType, true))
            {
                // Use the final row of the Game page's left checkbox column.
                if (Convert.ToString(DialogueView.Read(tag, "BoolSuboption")) == "CLIP_CURSOR") { selected = tag; break; }
            }
            if (selected == null) { Debug.LogWarning("[DeadfireDialog] CageCursor option template not found."); return; }
            GameObject clone = UnityEngine.Object.Instantiate(selected.gameObject);
            try
            {
            clone.name = "Vertical Dialogue Interface";
            clone.transform.SetParent(selected.transform.parent, false);
            clone.transform.localPosition = selected.transform.localPosition + new Vector3(0, -32, 0);
            clone.transform.localRotation = selected.transform.localRotation;
            clone.transform.localScale = selected.transform.localScale;
            Component tagCopy = clone.GetComponent(tagType);
            Component check = DialogueView.Read(tagCopy, "Checkbox") as Component;
            Component button = DialogueView.Read(tagCopy, "Button") as Component;
            Component label = DialogueView.Read(tagCopy, "Label") as Component;
            if ((check == null && button == null) || label == null) { Destroy(clone); Debug.LogWarning("[DeadfireDialog] Native option has no toggle/label."); return; }
            // Strip the cloned game's option binding before its Start/Reload.
            ((Behaviour)tagCopy).enabled = false;
            DestroyImmediate(tagCopy);
            foreach (Component child in clone.GetComponentsInChildren<Component>(true))
                if (child != null && (child.GetType().Name == "UIOptionsTooltipTrigger" || child.GetType().Name == "UISavedOption" || child.GetType().Name == "UIOptionsSetter" || child.GetType().GetField("DatabaseString", Flags) != null)) DestroyImmediate(child);
            if (check != null)
            {
                Set(check, "eventReceiver", null); Set(check, "functionName", "");
                Set(check, "radioButtonRoot", null);
                Set(check, "onStateChange", null); Set(check, "onStateChangeUser", null);
            }
            Set(label, "text", "Vertical Dialogue Interface");
            DialogueStyleOption option = clone.AddComponent<DialogueStyleOption>(); option.checkbox = check; option.toggleButton = button;
            if (check != null)
            {
                FieldInfo callback = check.GetType().GetField("onStateChangeUser", Flags);
                callback.SetValue(check, Delegate.CreateDelegate(callback.FieldType, option, "OnChecked"));
            }
            else
            {
                FieldInfo callback = button.GetType().GetField("OnToggleStateChanged", Flags);
                MethodInfo add = callback.FieldType.GetMethod("op_Addition", BindingFlags.Public | BindingFlags.Static);
                // Mono requires the exact native sender type here; it does not
                // bind an Action<NativeButton,bool> to (Component,bool).
                MethodInfo target = typeof(DialogueStyleOption).GetMethod("OnNativeToggled", Flags).MakeGenericMethod(button.GetType());
                Delegate handler = Delegate.CreateDelegate(add.GetParameters()[1].ParameterType, option, target);
                callback.SetValue(button, add.Invoke(null, new object[] { callback.GetValue(button), handler }));
            }
            // The cloned label's native TextCollider has no copied runtime
            // delegate. Wire it to this checkbox, never to the source option.
            Type listenerType = Type.GetType("UIEventListener, Assembly-CSharp");
            foreach (Component listener in clone.GetComponentsInChildren(listenerType, true))
            {
                if (listener.gameObject.name != "TextCollider") continue;
                FieldInfo click = listenerType.GetField("onClick", Flags);
                object current = click.GetValue(listener);
                MethodInfo add = click.FieldType.GetMethod("op_Addition", BindingFlags.Public | BindingFlags.Static);
                click.SetValue(listener, add.Invoke(null, new object[] { current, (Action<GameObject>)option.OnLabelClick }));
            }
            option.Sync();
            Debug.Log("[DeadfireDialog] Native Game options dialogue style checkbox added.");
            }
            catch { clone.SetActive(false); Destroy(clone); throw; }
        }
        private static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Flags);
            if (field != null) { field.SetValue(target, value); return; }
            PropertyInfo property = target.GetType().GetProperty(name, Flags);
            if (property != null && property.CanWrite) property.SetValue(target, value, null);
        }
        public void OnChecked(GameObject sender, bool enabled) { if (!syncing) Hooks.SetStyle(enabled); }
        private void OnNativeToggled<T>(T sender, bool enabled) where T : Component { if (!syncing) Hooks.SetStyle(enabled); }
        public void OnLabelClick(GameObject sender)
        {
            if (checkbox != null) MethodDispatch.Invoke(checkbox, "OnClick");
            else if (toggleButton != null) MethodDispatch.Invoke(toggleButton, "OnChildClick", sender);
        }
        private void Sync()
        {
            if (checkbox == null && toggleButton == null) return;
            syncing = true;
            if (checkbox != null) MethodDispatch.Invoke(checkbox, "SetNoCallback", DialoguePreferences.Enabled);
            else MethodDispatch.Invoke(toggleButton, "SetToggleStateNoCallback", DialoguePreferences.Enabled);
            syncing = false;
        }
        private void OnEnable() { Sync(); }
        private void Update()
        {
            object current = checkbox != null ? DialogueView.Read(checkbox, "isChecked") : DialogueView.Read(toggleButton, "ToggleState");
            if (!System.Object.Equals(current, DialoguePreferences.Enabled)) Sync();
        }
    }
}
