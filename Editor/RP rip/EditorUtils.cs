using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Copied from https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.core/Editor/CoreEditorUtils.cs
// To prevent dependancy from editor code
public static class CompositeEditorUtils
{
    /// <summary>Draw a splitter separator</summary>
    /// <param name="isBoxed">[Optional] add margin if the splitter is boxed</param>
    public static void DrawSplitter(bool isBoxed = false)
    {
        var rect = GUILayoutUtility.GetRect(1f, 1f);
        float xMin = rect.xMin;

        // Splitter rect should be full-width
        rect = ToFullWidth(rect);

        if (isBoxed)
        {
            rect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
            rect.width -= 1;
        }

        if (Event.current.type != EventType.Repaint)
            return;

        EditorGUI.DrawRect(rect, !EditorGUIUtility.isProSkin
            ? new Color(0.6f, 0.6f, 0.6f, 1.333f)
            : new Color(0.12f, 0.12f, 0.12f, 1.333f));
    }

    private static Rect ToFullWidth(Rect rect)
    {
        rect.xMin = 0f;
        rect.width += 4f;
        return rect;
    }

    public static bool DrawHeaderToggle(GUIContent title, SerializedProperty group, SerializedProperty activeField, Action<Vector2> contextAction, Func<bool> hasMoreOptions = null, Action toggleMoreOptions = null, string documentationURL = null)
    {
        GetHeaderToggleRects(out Rect labelRect, out Rect foldoutRect, out Rect toggleRect, out Rect backgroundRect);
        DrawBackground(backgroundRect);

        // Title
        using (new EditorGUI.DisabledScope(!activeField.boolValue))
            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

        // Foldout
        group.serializedObject.Update();
        group.isExpanded = GUI.Toggle(foldoutRect, group.isExpanded, GUIContent.none, EditorStyles.foldout);
        group.serializedObject.ApplyModifiedProperties();

        // Active checkbox
        activeField.serializedObject.Update();
        activeField.boolValue = GUI.Toggle(toggleRect, activeField.boolValue, GUIContent.none, CompositeEditorStyles.smallTickbox);
        activeField.serializedObject.ApplyModifiedProperties();

        contextAction = ContextMenu(title, contextAction, hasMoreOptions, toggleMoreOptions, documentationURL, labelRect);
        group.isExpanded = HandleEvents(contextAction, backgroundRect, group.isExpanded);

        return group.isExpanded;
    }

    private static bool HandleEvents(Action<Vector2> contextAction, Rect backgroundRect, bool expanded)
    {
        // Handle events
        var e = Event.current;

        if (e.type == EventType.MouseDown)
        {
            if (backgroundRect.Contains(e.mousePosition))
            {
                // Left click: Expand/Collapse
                if (e.button == 0)
                    expanded = !expanded;
                // Right click: Context menu
                else if (contextAction != null)
                    contextAction(e.mousePosition);

                e.Use();
            }
        }

        return expanded;
    }

    private static void DrawBackground(Rect backgroundRect)
    {
        // Background
        float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
        EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));
    }

    private static void GetHeaderToggleRects(out Rect labelRect, out Rect foldoutRect, out Rect toggleRect, out Rect backgroundRect)
    {
        backgroundRect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, 17f));

        labelRect = backgroundRect;
        labelRect.xMin += 32f;
        labelRect.xMax -= 20f + 16 + 5;

        foldoutRect = backgroundRect;
        foldoutRect.y += 1f;
        foldoutRect.width = 13f;
        foldoutRect.height = 13f;

        toggleRect = backgroundRect;
        toggleRect.x += 16f;
        toggleRect.y += 2f;
        toggleRect.width = 13f;
        toggleRect.height = 13f;

        // Background rect should be full-width
        backgroundRect = ToFullWidth(backgroundRect);
    }

    private static Action<Vector2> ContextMenu(GUIContent title, Action<Vector2> contextAction, Func<bool> hasMoreOptions, Action toggleMoreOptions, string documentationURL, Rect labelRect)
    {
        // Context menu
        var contextMenuIcon = CompositeEditorStyles.contextMenuIcon.image;
        var contextMenuRect = new Rect(labelRect.xMax + 3f + 16 + 5, labelRect.y + 1f, 16, 16);

        if (contextAction == null && hasMoreOptions != null)
        {
            // If no contextual menu add one for the additional properties.
            contextAction = pos => OnContextClick(pos, hasMoreOptions, toggleMoreOptions);
        }

        if (contextAction != null)
        {
            if (GUI.Button(contextMenuRect, CompositeEditorStyles.contextMenuIcon, CompositeEditorStyles.contextMenuStyle))
                contextAction(new Vector2(contextMenuRect.x, contextMenuRect.yMax));
        }

        // Documentation button
        ShowHelpButton(contextMenuRect, documentationURL, title);
        return contextAction;
    }

    static void ShowHelpButton(Rect contextMenuRect, string documentationURL, GUIContent title)
    {
        if (string.IsNullOrEmpty(documentationURL))
            return;

        var documentationRect = contextMenuRect;
        documentationRect.x -= 16 + 2;

        var documentationIcon = new GUIContent(CompositeEditorStyles.iconHelp, $"Open Reference for {title.text}.");

        if (GUI.Button(documentationRect, documentationIcon, CompositeEditorStyles.iconHelpStyle))
            Help.BrowseURL(documentationURL);
    }

    static void OnContextClick(Vector2 position, Func<bool> hasMoreOptions, Action toggleMoreOptions)
    {
        var menu = new GenericMenu();

        menu.AddItem(EditorGUIUtility.TrTextContent("Show Additional Properties"), hasMoreOptions.Invoke(), () => toggleMoreOptions.Invoke());
        menu.AddItem(EditorGUIUtility.TrTextContent("Show All Additional Properties..."), false, () => Open());

        menu.DropDown(new Rect(position, Vector2.zero));
    }

    public static readonly string corePreferencePath = "Preferences/Composite Data";

    public static void Open()
    {
        SettingsService.OpenUserPreferences(corePreferencePath);
    }

    #region IconAndSkin
    static CompositeEditorUtils()
    {
        LoadSkinAndIconMethods();
    }

    internal enum Skin
    {
        Auto,
        Personnal,
        Professional,
    }

    static Func<int> GetInternalSkinIndex;
    static Func<float> GetGUIStatePixelsPerPoint;
    static Func<Texture2D, float> GetTexturePixelPerPoint;
    static Action<Texture2D, float> SetTexturePixelPerPoint;

    static void LoadSkinAndIconMethods()
    {
        var internalSkinIndexInfo = typeof(EditorGUIUtility).GetProperty("skinIndex", BindingFlags.NonPublic | BindingFlags.Static);
        var internalSkinIndexLambda = Expression.Lambda<Func<int>>(Expression.Property(null, internalSkinIndexInfo));
        GetInternalSkinIndex = internalSkinIndexLambda.Compile();

        var guiStatePixelsPerPointInfo = typeof(GUIUtility).GetProperty("pixelsPerPoint", BindingFlags.NonPublic | BindingFlags.Static);
        var guiStatePixelsPerPointLambda = Expression.Lambda<Func<float>>(Expression.Property(null, guiStatePixelsPerPointInfo));
        GetGUIStatePixelsPerPoint = guiStatePixelsPerPointLambda.Compile();

        var pixelPerPointParam = Expression.Parameter(typeof(float), "pixelPerPoint");
        var texture2DProperty = Expression.Parameter(typeof(Texture2D), "texture2D");
        var texture2DPixelsPerPointInfo = typeof(Texture2D).GetProperty("pixelsPerPoint", BindingFlags.NonPublic | BindingFlags.Instance);
        var texture2DPixelsPerPointProperty = Expression.Property(texture2DProperty, texture2DPixelsPerPointInfo);
        var texture2DGetPixelsPerPointLambda = Expression.Lambda<Func<Texture2D, float>>(texture2DPixelsPerPointProperty, texture2DProperty);
        GetTexturePixelPerPoint = texture2DGetPixelsPerPointLambda.Compile();
        var texture2DSetPixelsPerPointLambda = Expression.Lambda<Action<Texture2D, float>>(Expression.Assign(texture2DPixelsPerPointProperty, pixelPerPointParam), texture2DProperty, pixelPerPointParam);
        SetTexturePixelPerPoint = texture2DSetPixelsPerPointLambda.Compile();
    }

    static Skin currentSkin
            => GetInternalSkinIndex() == 0 ? Skin.Personnal : Skin.Professional;

    internal static void TryToFixFilterMode(float pixelsPerPoint, Texture2D icon)
    {
        if (icon != null &&
            !Mathf.Approximately(GetTexturePixelPerPoint(icon), pixelsPerPoint) && //scaling are different
            !Mathf.Approximately(pixelsPerPoint % 1, 0)) //screen scaling is non-integer
        {
            icon.filterMode = FilterMode.Bilinear;
        }
    }

    internal static Texture2D LoadIcon(string path, string name, string extention = ".png", bool forceLowRes = false)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name))
            return null;

        string prefix = "";

        var skin = currentSkin;
        if (skin == Skin.Professional)
            prefix = "d_";

        Texture2D icon = null;
        float pixelsPerPoint = GetGUIStatePixelsPerPoint();
        if (pixelsPerPoint > 1.0f && !forceLowRes)
        {
            icon = EditorGUIUtility.Load($"{path}/{prefix}{name}@2x{extention}") as Texture2D;
            if (icon == null && !string.IsNullOrEmpty(prefix))
                icon = EditorGUIUtility.Load($"{path}/{name}@2x{extention}") as Texture2D;
            if (icon != null)
                SetTexturePixelPerPoint(icon, 2.0f);
        }

        if (icon == null)
            icon = EditorGUIUtility.Load($"{path}/{prefix}{name}{extention}") as Texture2D;

        if (icon == null && !string.IsNullOrEmpty(prefix))
            icon = EditorGUIUtility.Load($"{path}/{name}{extention}") as Texture2D;

        TryToFixFilterMode(pixelsPerPoint, icon);

        return icon;
    }
    #endregion

    internal static Texture2D FindTexture(string name)
    {
        float pixelsPerPoint = GetGUIStatePixelsPerPoint();
        Texture2D icon = pixelsPerPoint > 1.0f
            ? EditorGUIUtility.FindTexture($"{name}@2x")
            : EditorGUIUtility.FindTexture(name);

        TryToFixFilterMode(pixelsPerPoint, icon);

        return icon;
    }

    public static bool TryGetHelpURL(Type type, out string url)
    {
        var attribute = type.GetCustomAttribute<HelpURLAttribute>(false);
        url = attribute?.URL;
        return attribute != null;
    }
}

// Copied from https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.core/Editor/CoreEditorStyles.cs
public static class CompositeEditorStyles
{
    static System.Lazy<GUIStyle> m_SmallTickbox = new(() => new GUIStyle("ShurikenToggle"));
    /// <summary>Style for a small checkbox</summary>
    public static GUIStyle smallTickbox => m_SmallTickbox.Value;

    static System.Lazy<GUIStyle> m_ContextMenuStyle = new(() => new GUIStyle("IconButton"));
    /// <summary>Context Menu button style</summary>
    public static GUIStyle contextMenuStyle => m_ContextMenuStyle.Value;

    /// <summary>Context Menu button icon</summary>
    public static readonly GUIContent contextMenuIcon;

    /// <summary> PaneOption icon for dark skin</summary>
    static readonly Texture2D paneOptionsIconDark;
    /// <summary> PaneOption icon for light skin</summary>
    static readonly Texture2D paneOptionsIconLight;
    /// <summary> PaneOption icon </summary>
    public static Texture2D paneOptionsIcon => EditorGUIUtility.isProSkin ? paneOptionsIconDark : paneOptionsIconLight;
    static CompositeEditorStyles()
    {
        paneOptionsIconDark = CompositeEditorUtils.LoadIcon("Builtin Skins/DarkSkin/Images", "pane options", ".png");
        paneOptionsIconDark.name = "pane options dark skin";
        paneOptionsIconLight = CompositeEditorUtils.LoadIcon("Builtin Skins/LightSkin/Images", "pane options", ".png");
        paneOptionsIconLight.name = "pane options light skin";

        const string contextTooltip = ""; // To be defined (see with UX)
        contextMenuIcon = new GUIContent(paneOptionsIcon, contextTooltip);


        iconHelp = CompositeEditorUtils.FindTexture("_Help");
    }


    /// <summary> Help icon </summary>
    public static readonly Texture2D iconHelp;
    /// <summary>Help icon style</summary>
    public static GUIStyle iconHelpStyle => GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
}

