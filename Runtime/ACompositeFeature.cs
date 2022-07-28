using System;
using UnityEngine;

[ExcludeFromPreset]
public abstract class ACompositeFeature : ScriptableObject, IDisposable
{
    [SerializeField, HideInInspector] private bool m_Active = true;
    /// <summary>
    /// Returns the state of the ACompositeFeature (true: the feature is active, false: the feature is inactive). Use the method ACompositeFeature.SetActive to change the value of this variable.
    /// </summary>
    public bool isActive => m_Active;

    /// <summary>
    /// Initializes this feature's resources. This is called every time serialization happens.
    /// </summary>
    public abstract void Create();


    void OnEnable()
    {
        Create();
    }

    void OnValidate()
    {
        Create();
    }

    /// <summary>
    /// Sets the state of ACompositeFeature (true: the feature is active, false: the feature is inactive).
    /// If the feature is active, it is added to the renderer it is attached to, otherwise the feature is skipped while rendering.
    /// </summary>
    /// <param name="active">The true value activates the ACompositeFeature and the false value deactivates it.</param>
    public void SetActive(bool active)
    {
        m_Active = active;
    }

    /// <summary>
    /// Disposable pattern implementation.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}