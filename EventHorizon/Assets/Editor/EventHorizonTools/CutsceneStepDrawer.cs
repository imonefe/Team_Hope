using UnityEditor;
using UnityEngine;

/// <summary>
/// CutsceneStep의 타입(줌/포커스/대사 등)에 따라 관련 필드만 보여주는 인스펙터 커스텀 드로어.
/// 이게 없으면 모든 스텝에 8종류 필드가 전부 다 보여서 헷갈리기 때문에 필수.
/// </summary>
[CustomPropertyDrawer(typeof(CutsceneStep))]
public class CutsceneStepDrawer : PropertyDrawer
{
    static float LineH => EditorGUIUtility.singleLineHeight;
    static float Spacing => EditorGUIUtility.standardVerticalSpacing;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var typeProp = property.FindPropertyRelative("type");
        var labelProp = property.FindPropertyRelative("label");

        float y = position.y;

        var typeRect = new Rect(position.x, y, position.width, LineH);
        EditorGUI.PropertyField(typeRect, typeProp, new GUIContent("타입"));
        y += LineH + Spacing;

        var labelRect = new Rect(position.x, y, position.width, LineH);
        EditorGUI.PropertyField(labelRect, labelProp, new GUIContent("메모 (선택)"));
        y += LineH + Spacing;

        foreach (var fieldName in GetFieldNames((CutsceneStepType)typeProp.enumValueIndex))
        {
            var fp = property.FindPropertyRelative(fieldName);
            if (fp == null) continue;

            float h = EditorGUI.GetPropertyHeight(fp, true);
            var r = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(r, fp, true);
            y += h + Spacing;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float total = (LineH + Spacing) * 2f; // 타입 + 메모

        var typeProp = property.FindPropertyRelative("type");
        foreach (var fieldName in GetFieldNames((CutsceneStepType)typeProp.enumValueIndex))
        {
            var fp = property.FindPropertyRelative(fieldName);
            if (fp == null) continue;
            total += EditorGUI.GetPropertyHeight(fp, true) + Spacing;
        }
        return total;
    }

    static string[] GetFieldNames(CutsceneStepType type)
    {
        switch (type)
        {
            case CutsceneStepType.Wait:
                return new[] { "waitSeconds" };
            case CutsceneStepType.ZoomTo:
                return new[] { "zoomSize", "zoomDuration" };
            case CutsceneStepType.FocusOn:
                return new[] { "focusTarget", "focusDuration" };
            case CutsceneStepType.PlayAnimatorTrigger:
                return new[] { "animator", "triggerName" };
            case CutsceneStepType.SlowMotion:
                return new[] { "slowMotionScale", "slowMotionHoldSeconds", "slowMotionRampSeconds" };
            case CutsceneStepType.Shake:
                return new[] { "shakeDuration", "shakeStrength" };
            case CutsceneStepType.Dialogue:
                return new[] { "speakerName", "dialogueText", "dialogueHoldSeconds" };
            default:
                return new string[0];
        }
    }
}
