using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

class CompositeFeatureProvider : CompositeFilterWindow.IProvider
{
    class FeatureElement : CompositeFilterWindow.Element
    {
        public Type type;
    }

    readonly CompositeDataEditor m_Editor;
    public Vector2 position { get; set; }

    public CompositeFeatureProvider(CompositeDataEditor editor)
    {
        m_Editor = editor;
    }

    public void CreateComponentTree(List<CompositeFilterWindow.Element> tree)
    {
        tree.Add(new CompositeFilterWindow.GroupElement(0, "Features"));
        AProtoCompositeData data = m_Editor.target as AProtoCompositeData;
        var types = TypeCache.GetTypesDerivedFrom(data.GetFeatureType());
        foreach (var type in types)
        {
            if (data.DuplicateFeatureCheck(type))
            {
                continue;
            }

            string path = GetMenuNameFromType(type);
            tree.Add(new FeatureElement
            {
                content = new GUIContent(path),
                level = 1,
                type = type
            });
        }
    }

    public bool GoToChild(CompositeFilterWindow.Element element, bool addIfComponent)
    {
        if (element is FeatureElement featureElement)
        {
            m_Editor.AddComponent(featureElement.type.Name);
            return true;
        }

        return false;
    }

    string GetMenuNameFromType(Type type)
    {
        string path;
        if (!m_Editor.GetCustomTitle(type, out path))
        {
            path = ObjectNames.NicifyVariableName(type.Name);
        }

        if (type.Namespace != null)
        {
            if (type.Namespace.Contains("Experimental"))
                path += " (Experimental)";
        }

        return path;
    }

}