using UnityEditor;

[CustomEditor(typeof(ACompositeFeature), true)]
    public class CompositeFeatureEditor : Editor
{
    /// <inheritdoc/>
    public override void OnInspectorGUI()
    {
        DrawPropertiesExcluding(serializedObject, "m_Script", "m_name");
    }
}