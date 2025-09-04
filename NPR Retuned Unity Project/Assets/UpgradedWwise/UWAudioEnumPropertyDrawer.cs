using UnityEngine;
using UnityEditor;
using System;
#if UNITY_EDITOR 
public abstract class AudioEnumDrawer<TEnum, TPicker> : PropertyDrawer
    where TEnum : Enum
    where TPicker : UWPopupPicker
{
    protected abstract TEnum DefaultEnum { get; } 
    protected virtual string FormatEnumLabel(TEnum enumValue)
    {
        return enumValue.ToString();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.Enum)
        {
            EditorGUI.BeginProperty(position, label, property);

            int enumValue = property.enumValueFlag;

            if (!Enum.IsDefined(typeof(TEnum), enumValue))
            {
                enumValue = Convert.ToInt32(DefaultEnum);
                property.enumValueFlag = enumValue;
                property.serializedObject.ApplyModifiedProperties();
            }

            Rect labelRect = new Rect(position.x, position.y, position.width * 0.4f, position.height);
            Rect buttonRect = new Rect(position.x + labelRect.width, position.y, position.width * 0.6f, position.height);

            EditorGUI.PrefixLabel(labelRect, label);

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(FormatEnumLabel((TEnum)(object)enumValue)), FocusType.Keyboard))
            {
                typeof(TPicker).GetMethod($"Open{typeof(TPicker).ToString().Replace("UW", string.Empty)}")?.Invoke(null, new object[] { property, buttonRect });
            }

            EditorGUI.EndProperty();
        }
        else
        {
            EditorGUI.PropertyField(position, property, label);
        }
    }
}

[CustomPropertyDrawer(typeof(AudioEvent))]
public class EventEnumPickerDrawer : AudioEnumDrawer<AudioEvent, UWEventPicker>
{
    protected override AudioEvent DefaultEnum => AudioEvent.None;
}

[CustomPropertyDrawer(typeof(AudioState))]
public class StateEnumPickerDrawer : AudioEnumDrawer<AudioState, UWStatePicker>
{
    protected override AudioState DefaultEnum => AudioState.None;
    protected override string FormatEnumLabel(AudioState enumValue)
    {
        return enumValue.ToString().Replace("_BREAK_", " > ");
    }
}

[CustomPropertyDrawer(typeof(AudioSwitch))]
public class SwitchEnumPickerDrawer : AudioEnumDrawer<AudioSwitch, UWSwitchPicker>
{
    protected override AudioSwitch DefaultEnum => AudioSwitch.None;
    protected override string FormatEnumLabel(AudioSwitch enumValue)
    {
        return enumValue.ToString().Replace("_BREAK_", " > ");
    }
}

[CustomPropertyDrawer(typeof(AudioTrigger))]
public class TriggerEnumPickerDrawer : AudioEnumDrawer<AudioTrigger, UWTriggerPicker>
{
    protected override AudioTrigger DefaultEnum => AudioTrigger.None;
}

[CustomPropertyDrawer(typeof(AudioRTPC))]
public class RTPCEnumPickerDrawer : AudioEnumDrawer<AudioRTPC, UWRTPCPicker>
{
    protected override AudioRTPC DefaultEnum => AudioRTPC.None;
}
#endif