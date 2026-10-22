using UnityEditor;
using UnityEngine;

/// <summary>
/// CutsceneStep의 타입(줌/포커스/대사 등)에 따라 관련 필드만 보여주는 인스펙터 커스텀 드로어.
/// 이게 없으면 모든 스텝에 18종류 필드가 전부 다 보여서 헷갈리기 때문에 필수.
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

        // [2026-10-15 추가] 기다리지 않고 다음 스텝으로 (모든 타입 공통)
        var bgProp = property.FindPropertyRelative("runInBackground");
        if (bgProp != null)
        {
            var bgRect = new Rect(position.x, y, position.width, LineH);
            EditorGUI.PropertyField(bgRect, bgProp, new GUIContent("기다리지 않고 다음 스텝으로"));
            y += LineH + Spacing;
        }

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
        float total = (LineH + Spacing) * 3f; // 타입 + 메모 + 기다리지 않음

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
                return new[] { "focusOnPlayer", "focusOnSpawned", "spawnedChildName", "focusTarget", "focusOffset", "focusDuration", "exclusiveFocus" };
            case CutsceneStepType.PlayAnimatorTrigger:
                return new[] { "animator", "triggerName" };
            case CutsceneStepType.SlowMotion:
                return new[] { "slowMotionScale", "slowMotionHoldSeconds", "slowMotionRampSeconds" };
            case CutsceneStepType.Shake:
                return new[] { "shakeDuration", "shakeStrength" };
            case CutsceneStepType.Dialogue:
                return new[] { "speakerName", "dialogueText", "dialogueHoldSeconds" };
            // [2026-10-15 추가] 보스 컷씬용 스텝
            case CutsceneStepType.LockPlayer:
            case CutsceneStepType.UnlockPlayer:
                return new string[0];
            case CutsceneStepType.Letterbox:
                return new[] { "letterboxShow", "letterboxHeight", "letterboxDuration" };
            case CutsceneStepType.SpineAnimation:
                return new[] { "spineTarget", "spineAnimationName", "spineLoop", "spineNextAnimation", "waitForSpine" };
            case CutsceneStepType.SpawnEffect:
                return new[] { "effectPrefab", "effectAtPlayer", "effectAnchor", "effectOffset", "effectScale", "effectSpeed", "effectLifetime", "effectHierarchyScale", "effectSortingOrder", "effectSpineSortingOrder", "effectHideChildren", "effectFlipSprites" };
            case CutsceneStepType.ZoomPunch:
                return new[] { "punchAmount", "punchInSeconds", "punchHoldSeconds", "punchOutSeconds" };
            case CutsceneStepType.Flash:
                return new[] { "flashColor", "flashPeak", "flashDuration" };
            case CutsceneStepType.ShakeRamp:
                return new[] { "shakeRampFrom", "shakeStrength", "shakeDuration" };
            case CutsceneStepType.KillPlayer:
                return new[] { "killSource", "killKnockback", "killFadeSeconds", "gameOverDelay" };
            case CutsceneStepType.FadeScreen:
                return new[] { "fadeColor", "fadeTo", "fadeDuration" };
            case CutsceneStepType.ProjectileShot:
                return new[] { "projectileFollowParticles", "projectileFlightTimeScale", "projectileTimeout", "projectileChildName", "projectileSeconds", "projectileTargetOffset", "projectileCameraFollow", "impactPrefab", "impactScale", "projectileTrailWidth", "projectileTrailTime", "projectileColor", "projectileHeadSize" };
            // [2026-10-22] 검사의 일격 (플레이어를 지나는 선으로 화면 가르기)
            case CutsceneStepType.SwordSlashKill:
                return new[] { "slashFrame", "slashTimeout", "slashAngle", "slashCenterOffset", "cutDelay" };
            default:
                return new string[0];
        }
    }
}
