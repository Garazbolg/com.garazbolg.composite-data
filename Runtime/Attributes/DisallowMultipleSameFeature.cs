using System;
using System.Reflection;

/// <summary>
///   <para>Prevents <c>ACompisteFeature</c> of same type to be added more than once to a ACompositeData.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited=true)]
public class DisallowMultipleSameFeature : Attribute
{
    /// <summary>
    /// Set the custom title for feature.
    /// </summary>
    public string customTitle { private set; get; }

    /// <summary>
    /// Constructor for the attribute to prevent <c>CompositeFeatures</c> of same type to be added more than once to a CompositeData
    /// </summary>
    /// <param name="customTitle">Sets the custom title for feature.</param>
    public DisallowMultipleSameFeature(string customTitle = null)
    {
        this.customTitle = customTitle;
    }

    public static bool DuplicateCustomTitleCheck(DisallowMultipleSameFeature dmsf, System.Type t2)
    {
        var isSingleFeature = t2.GetCustomAttribute<DisallowMultipleSameFeature>();

        return isSingleFeature != null && isSingleFeature.customTitle.Equals(dmsf.customTitle);
    }
}