#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SceneFlipManager))]
public class SceneFlipManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SceneFlipManager manager = (SceneFlipManager)target;

        GUILayout.Space(12);
        GUILayout.Label("编辑器预览", EditorStyles.boldLabel);

        if (GUILayout.Button("预览 / 切换 左右翻转"))
        {
            PreviewFlip(manager, true, false);
        }

        if (GUILayout.Button("预览 / 切换 上下翻转"))
        {
            PreviewFlip(manager, false, true);
        }

        if (GUILayout.Button("预览 / 切换 左右 + 上下翻转"))
        {
            PreviewFlip(manager, true, true);
        }

        GUILayout.Space(6);

        if (GUILayout.Button("恢复为正常方向"))
        {
            ResetPreview(manager);
        }

        GUILayout.Space(6);

        EditorGUILayout.HelpBox(
            "这些按钮只是在编辑地图时帮你预览 SceneFlipRoot 的翻转状态。\n" +
            "如果你点了预览后保存场景，当前翻转状态也会被保存。\n" +
            "发布前建议点一次“恢复为正常方向”。",
            MessageType.Info
        );
    }

    private void PreviewFlip(SceneFlipManager manager, bool flipX, bool flipY)
    {
        if (manager == null)
            return;

        Transform root = manager.sceneRoot;

        if (root == null)
        {
            Debug.LogWarning("SceneFlipManager 没有设置 Scene Root。");
            return;
        }

        Bounds beforeBounds;
        bool hasBounds = TryGetSceneBounds(root, out beforeBounds);

        Undo.RecordObject(root, "Preview Scene Flip");

        Vector3 scale = root.localScale;

        if (flipX)
            scale.x *= -1f;

        if (flipY)
            scale.y *= -1f;

        root.localScale = scale;

        Physics2D.SyncTransforms();

        if (manager.keepSceneCenterInPlace && hasBounds)
        {
            KeepBoundsCenterInPlace(root, beforeBounds);
        }

        Physics2D.SyncTransforms();

        EditorUtility.SetDirty(root);
        SceneView.RepaintAll();
    }

    private void ResetPreview(SceneFlipManager manager)
    {
        if (manager == null)
            return;

        Transform root = manager.sceneRoot;

        if (root == null)
        {
            Debug.LogWarning("SceneFlipManager 没有设置 Scene Root。");
            return;
        }

        Bounds beforeBounds;
        bool hasBounds = TryGetSceneBounds(root, out beforeBounds);

        Undo.RecordObject(root, "Reset Scene Flip Preview");

        Vector3 scale = root.localScale;

        scale.x = Mathf.Abs(scale.x);
        scale.y = Mathf.Abs(scale.y);
        scale.z = Mathf.Abs(scale.z);

        root.localScale = scale;

        Physics2D.SyncTransforms();

        if (manager.keepSceneCenterInPlace && hasBounds)
        {
            KeepBoundsCenterInPlace(root, beforeBounds);
        }

        Physics2D.SyncTransforms();

        EditorUtility.SetDirty(root);
        SceneView.RepaintAll();
    }

    private bool TryGetSceneBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.zero);

        bool hasBounds = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null)
                continue;

            Bounds cb = c.bounds;

            if (cb.size.sqrMagnitude <= 0.000001f)
                continue;

            if (!hasBounds)
            {
                bounds = cb;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(cb);
            }
        }

        return hasBounds;
    }

    private void KeepBoundsCenterInPlace(Transform root, Bounds beforeBounds)
    {
        Bounds afterBounds;
        bool hasAfterBounds = TryGetSceneBounds(root, out afterBounds);

        if (!hasAfterBounds)
            return;

        Undo.RecordObject(root, "Keep Scene Center In Place");

        Vector3 offset = beforeBounds.center - afterBounds.center;
        root.position += offset;

        EditorUtility.SetDirty(root);
    }
}
#endif