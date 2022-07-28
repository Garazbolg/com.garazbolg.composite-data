using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

[System.Serializable]
public abstract class AProtoCompositeData : ScriptableObject
{
    internal bool isInvalidated { get; set; }

    public abstract bool DuplicateFeatureCheck(System.Type type);

    /// <summary>
    /// Use SetDirty when changing seeings in the ScriptableRendererData.
    /// It will rebuild the render passes with the new data.
    /// </summary>
    public new void SetDirty()
    {
        isInvalidated = true;
    }

    public abstract System.Type GetFeatureType();
    public abstract bool ValidateFeatures();
}

[System.Serializable]
public abstract class ACompositeData<U> : AProtoCompositeData where U:ACompositeFeature
{
    [SerializeField] public List<U> m_Features = new List<U>(10);
    [SerializeField] public List<long> m_FeatureMap = new List<long>(10);

    /// <summary>
    /// List of additional render pass features for this renderer.
    /// </summary>
    public List<U> features
    {
        get => m_Features;
    }

    protected virtual void OnValidate()
    {
        SetDirty();
#if UNITY_EDITOR
        if (m_Features.Contains(null))
            ValidateFeatures();
#endif
    }

    protected virtual void OnEnable()
    {
        SetDirty();
    }

    public override System.Type GetFeatureType()
    {
        return typeof(U);
    }

    /// <summary>
    /// Returns true if contains renderer feature with specified type.
    /// </summary>
    /// <typeparam name="T">Renderer Feature type.</typeparam>
    /// <returns></returns>
    internal bool TryGetRendererFeature<T>(out T rendererFeature) where T : ACompositeFeature
    {
        foreach (var target in features)
        {
            if (target.GetType() == typeof(T))
            {
                rendererFeature = target as T;
                return true;
            }
        }
        rendererFeature = null;
        return false;
    }

#if UNITY_EDITOR

    public override bool ValidateFeatures()
    {
        // Get all Subassets
        var subassets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(this));
        var linkedIds = new List<long>();
        var loadedAssets = new Dictionary<long, object>();
        var mapValid = m_FeatureMap != null && m_FeatureMap?.Count == m_Features?.Count;
        var debugOutput = $"{name}\nValid Sub-assets:\n";

        // Collect valid, compiled sub-assets
        foreach (var asset in subassets)
        {
            if (asset == null || asset.GetType().BaseType != typeof(ACompositeFeature)) continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId);
            loadedAssets.Add(localId, asset);
            debugOutput += $"-{asset.name}\n--localId={localId}\n";
        }

        // Collect assets that are connected to the list
        for (var i = 0; i < m_Features?.Count; i++)
        {
            if (!m_Features[i]) continue;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_Features[i], out var guid, out long localId))
            {
                linkedIds.Add(localId);
            }
        }

        var mapDebug = mapValid ? "Linking" : "Map missing, will attempt to re-map";
        debugOutput += $"Feature List Status({mapDebug}):\n";

        // Try fix missing references
        for (var i = 0; i < m_Features?.Count; i++)
        {
            if (m_Features[i] == null)
            {
                if (mapValid && m_FeatureMap[i] != 0)
                {
                    var localId = m_FeatureMap[i];
                    loadedAssets.TryGetValue(localId, out var asset);
                    m_Features[i] = (U)asset;
                }
                else
                {
                    m_Features[i] = (U)GetUnusedAsset(ref linkedIds, ref loadedAssets);
                }
            }

            debugOutput += m_Features[i] != null ? $"-{i}:Linked\n" : $"-{i}:Missing\n";
        }

        UpdateMap();

        if (!m_Features.Contains(null))
            return true;

        Debug.LogError($"{name} is missing Features\nThis could be due to missing scripts or compile error.", this);
        return false;
    }

    public override bool DuplicateFeatureCheck(System.Type type)
    {
        var isSingleFeature = type.GetCustomAttribute<DisallowMultipleSameFeature>();
        if (isSingleFeature == null) return false;
        return m_Features
            .Select(renderFeature => renderFeature.GetType())
            .Any(t => t == type || DisallowMultipleSameFeature.DuplicateCustomTitleCheck(isSingleFeature,t));
    }

    private static object GetUnusedAsset(ref List<long> usedIds, ref Dictionary<long, object> assets)
    {
        foreach (var asset in assets)
        {
            var alreadyLinked = usedIds.Any(used => asset.Key == used);

            if (alreadyLinked)
                continue;

            usedIds.Add(asset.Key);
            return asset.Value;
        }

        return null;
    }

    private void UpdateMap()
    {
        if (m_FeatureMap.Count != m_Features.Count)
        {
            m_FeatureMap.Clear();
            m_FeatureMap.AddRange(new long[m_Features.Count]);
        }

        for (int i = 0; i < features.Count; i++)
        {
            if (m_Features[i] == null) continue;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_Features[i], out var guid, out long localId)) continue;

            m_FeatureMap[i] = localId;
        }
    }

#endif

}
