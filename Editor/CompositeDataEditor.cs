using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

[CustomEditor(typeof(ACompositeData<>), true)]
public class CompositeDataEditor : Editor
{
    class Styles
    {
        public static readonly GUIContent MissingFeature = new GUIContent("Missing RendererFeature",
            "Missing reference, due to compilation issues or missing files. you can attempt auto fix or choose to remove the feature.");

        public static GUIStyle BoldLabelSimple;

        static Styles()
        {
            BoldLabelSimple = new GUIStyle(EditorStyles.label);
            BoldLabelSimple.fontStyle = FontStyle.Bold;
        }
    }

    private SerializedProperty m_Features;
    private SerializedProperty m_FeaturesMap;
    private SerializedProperty m_FalseBool;
    [SerializeField] private bool falseBool = false;
    List<Editor> m_Editors = new List<Editor>();

    private void OnEnable()
    {
        m_Features = serializedObject.FindProperty("m_Features");
        m_FeaturesMap = serializedObject.FindProperty("m_FeatureMap");
        var editorObj = new SerializedObject(this);
        m_FalseBool = editorObj.FindProperty(nameof(falseBool));
        UpdateEditorList();
    }

    private void OnDisable()
    {
        ClearEditorsList();
    }

    /// <inheritdoc/>
    public override void OnInspectorGUI()
    {
        if (m_Features == null)
            OnEnable();
        else if (m_Features.arraySize != m_Editors.Count)
            UpdateEditorList();

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script","m_Features", "m_FeatureMap");
        DrawFeatureList();
    }

    private void DrawFeatureList()
    {
        EditorGUILayout.Space();

        if (m_Features.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No Features added", MessageType.Info);
        }
        else
        {
            //Draw List
            CompositeEditorUtils.DrawSplitter();
            for (int i = 0; i < m_Features.arraySize; i++)
            {
                SerializedProperty featuresProperty = m_Features.GetArrayElementAtIndex(i);
                DrawFeature(i, ref featuresProperty);
                CompositeEditorUtils.DrawSplitter();
            }
        }
        EditorGUILayout.Space();

        using (var hscope = new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Feature", EditorStyles.miniButton))
            {
                var r = hscope.rect;
                var pos = new Vector2(r.x + r.width / 2f, r.yMax + 18f);
                CompositeFilterWindow.Show(pos, new CompositeFeatureProvider(this));
            }
        }
    }

    internal bool GetCustomTitle(Type type, out string title)
    {
        var isSingleFeature = type.GetCustomAttribute<DisallowMultipleSameFeature>();
        if (isSingleFeature != null)
        {
            title = $"{type.Name} ({isSingleFeature.customTitle})";
            return title != null;
        }
        title = null;
        return false;
    }

    private bool GetTooltip(Type type, out string tooltip)
    {
        var attribute = type.GetCustomAttribute<TooltipAttribute>();
        if (attribute != null)
        {
            tooltip = attribute.tooltip;
            return true;
        }
        tooltip = string.Empty;
        return false;
    }

    private void DrawFeature(int index, ref SerializedProperty featureProperty)
    {
        Object featureObjRef = featureProperty.objectReferenceValue;
        if (featureObjRef != null)
        {
            bool hasChangedProperties = false;
            string title;

            bool hasCustomTitle = GetCustomTitle(featureObjRef.GetType(), out title);

            if (!hasCustomTitle)
            {
                title = featureObjRef.GetType().Name;
            }

            string tooltip;
            GetTooltip(featureObjRef.GetType(), out tooltip);

            string helpURL;
            CompositeEditorUtils.TryGetHelpURL(featureObjRef.GetType(), out helpURL);

            // Get the serialized object for the editor script & update it
            Editor featureEditor = m_Editors[index];
            SerializedObject serializedFeaturesEditor = featureEditor.serializedObject;
            serializedFeaturesEditor.Update();

            // Foldout header
            EditorGUI.BeginChangeCheck();
            SerializedProperty activeProperty = serializedFeaturesEditor.FindProperty("m_Active");
            bool displayContent = CompositeEditorUtils.DrawHeaderToggle(EditorGUIUtility.TrTextContent(title, tooltip), featureProperty, activeProperty, pos => OnContextClick(pos, index), null, null, helpURL);
            hasChangedProperties |= EditorGUI.EndChangeCheck();

            // ObjectEditor
            if (displayContent)
            {
                if (!hasCustomTitle)
                {
                    EditorGUI.BeginChangeCheck();
                    SerializedProperty nameProperty = serializedFeaturesEditor.FindProperty("m_Name");
                    if (EditorGUI.EndChangeCheck())
                    {
                        hasChangedProperties = true;

                        // We need to update sub-asset name
                        featureObjRef.name = nameProperty.stringValue;
                        AssetDatabase.SaveAssets();

                        // Triggers update for sub-asset name change
                        ProjectWindowUtil.ShowCreatedAsset(target);
                    }
                }

                EditorGUI.BeginChangeCheck();
                featureEditor.OnInspectorGUI();
                hasChangedProperties |= EditorGUI.EndChangeCheck();

                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            }

            // Apply changes and save if the user has modified any settings
            if (hasChangedProperties)
            {
                serializedFeaturesEditor.ApplyModifiedProperties();
                serializedObject.ApplyModifiedProperties();
                ForceSave();
            }
        }
        else
        {
            CompositeEditorUtils.DrawHeaderToggle(Styles.MissingFeature, featureProperty, m_FalseBool, pos => OnContextClick(pos, index));
            m_FalseBool.boolValue = false; // always make sure false bool is false
            EditorGUILayout.HelpBox(Styles.MissingFeature.tooltip, MessageType.Error);
            if (GUILayout.Button("Attempt Fix", EditorStyles.miniButton))
            {
                AProtoCompositeData data = target as AProtoCompositeData;
                data.ValidateFeatures();
            }
        }
    }

    private void OnContextClick(Vector2 position, int id)
    {
        var menu = new GenericMenu();

        if (id == 0)
            menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Up"));
        else
            menu.AddItem(EditorGUIUtility.TrTextContent("Move Up"), false, () => MoveComponent(id, -1));

        if (id == m_Features.arraySize - 1)
            menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Down"));
        else
            menu.AddItem(EditorGUIUtility.TrTextContent("Move Down"), false, () => MoveComponent(id, 1));

        menu.AddSeparator(string.Empty);
        menu.AddItem(EditorGUIUtility.TrTextContent("Remove"), false, () => RemoveComponent(id));

        menu.DropDown(new Rect(position, Vector2.zero));
    }

    internal void AddComponent(string type)
    {
        serializedObject.Update();

        ScriptableObject component = CreateInstance((string)type);
        component.name = $"{(string)type}";
        Undo.RegisterCreatedObjectUndo(component, "Add Feature");

        // Store this new effect as a sub-asset so we can reference it safely afterwards
        // Only when we're not dealing with an instantiated asset
        if (EditorUtility.IsPersistent(target))
        {
            AssetDatabase.AddObjectToAsset(component, target);
        }
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out var guid, out long localId);

        // Grow the list first, then add - that's how serialized lists work in Unity
        m_Features.arraySize++;
        SerializedProperty componentProp = m_Features.GetArrayElementAtIndex(m_Features.arraySize - 1);
        componentProp.objectReferenceValue = component;

        // Update GUID Map
        m_FeaturesMap.arraySize++;
        SerializedProperty guidProp = m_FeaturesMap.GetArrayElementAtIndex(m_FeaturesMap.arraySize - 1);
        guidProp.longValue = localId;
        UpdateEditorList();
        serializedObject.ApplyModifiedProperties();

        // Force save / refresh
        if (EditorUtility.IsPersistent(target))
        {
            ForceSave();
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void RemoveComponent(int id)
    {
        SerializedProperty property = m_Features.GetArrayElementAtIndex(id);
        Object component = property.objectReferenceValue;
        property.objectReferenceValue = null;

        Undo.SetCurrentGroupName(component == null ? "Remove Feature" : $"Remove {component.name}");

        // remove the array index itself from the list
        m_Features.DeleteArrayElementAtIndex(id);
        m_FeaturesMap.DeleteArrayElementAtIndex(id);
        UpdateEditorList();
        serializedObject.ApplyModifiedProperties();

        // Destroy the setting object after ApplyModifiedProperties(). If we do it before, redo
        // actions will be in the wrong order and the reference to the setting object in the
        // list will be lost.
        if (component != null)
        {
            Undo.DestroyObjectImmediate(component);

            ACompositeFeature feature = component as ACompositeFeature;
            feature?.Dispose();
        }

        // Force save / refresh
        ForceSave();
    }

    private void MoveComponent(int id, int offset)
    {
        Undo.SetCurrentGroupName("Move Feature");
        serializedObject.Update();
        m_Features.MoveArrayElement(id, id + offset);
        m_FeaturesMap.MoveArrayElement(id, id + offset);
        UpdateEditorList();
        serializedObject.ApplyModifiedProperties();

        // Force save / refresh
        ForceSave();
    }

    private string ValidateName(string name)
    {
        name = Regex.Replace(name, @"[^a-zA-Z0-9 ]", "");
        return name;
    }

    private void UpdateEditorList()
    {
        ClearEditorsList();
        for (int i = 0; i < m_Features.arraySize; i++)
        {
            m_Editors.Add(CreateEditor(m_Features.GetArrayElementAtIndex(i).objectReferenceValue));
        }
    }

    //To avoid leaking memory we destroy editors when we clear editors list
    private void ClearEditorsList()
    {
        for (int i = m_Editors.Count - 1; i >= 0; --i)
        {
            DestroyImmediate(m_Editors[i]);
        }
        m_Editors.Clear();
    }

    private void ForceSave()
    {
        EditorUtility.SetDirty(target);
    }
}