using System.Xml.Linq;

namespace ModsBeforeFriday.Core.Manifest;

/// <summary>
/// UI-independent AndroidManifest.xml editor ported from mbf-site/src/AndroidManifest.ts.
/// </summary>
public sealed class AndroidManifestDocument
{
    private const string AndroidNamespaceUri = "http://schemas.android.com/apk/res/android";
    private static readonly XNamespace Android = AndroidNamespaceUri;

    private XDocument _document;
    private XElement _manifest;
    private XElement _application;

    public AndroidManifestDocument(string xml)
    {
        (_document, _manifest, _application) = Parse(xml);
        RemoveDuplicateMetadata();
    }

    public IReadOnlyList<string> Permissions => GetNamedManifestChildren("uses-permission");

    public IReadOnlyList<string> Features => GetNamedManifestChildren("uses-feature");

    public IReadOnlyList<string> NativeLibraries => _application.Elements("uses-native-library")
        .Select(element => (string?)element.Attribute(Android + "name"))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Cast<string>()
        .ToArray();

    public IReadOnlyDictionary<string, string> Metadata => _application.Elements("meta-data")
        .Select(element => new
        {
            Name = (string?)element.Attribute(Android + "name"),
            Value = (string?)element.Attribute(Android + "value"),
        })
        .Where(item => item.Name is not null && item.Value is not null)
        .ToDictionary(item => item.Name!, item => item.Value!, StringComparer.Ordinal);

    public void LoadFrom(string xml)
    {
        (_document, _manifest, _application) = Parse(xml);
        RemoveDuplicateMetadata();
    }

    public AndroidManifestDocument Clone() => new(ToXml());

    public string ToXml() => _document.ToString(SaveOptions.DisableFormatting);

    /// <summary>Applies the same default patch-time manifest changes as mbf-site.</summary>
    public void ApplyPatchingDefaults()
    {
        _application.SetAttributeValue(Android + "debuggable", "true");
        _application.SetAttributeValue(Android + "hardwareAccelerated", "true");
        _application.SetAttributeValue(Android + "requestLegacyExternalStorage", "true");

        AddPermission("android.permission.MANAGE_EXTERNAL_STORAGE");
        AddPermission("android.permission.WRITE_EXTERNAL_STORAGE");
        AddPermission("android.permission.READ_EXTERNAL_STORAGE");
        SetMetadata("com.oculus.supportedDevices", "quest|quest2");
    }

    public bool IsOptionEnabled(ManifestOptionDefinition option)
    {
        ArgumentNullException.ThrowIfNull(option);
        var permissions = Permissions;
        var features = Features;
        var metadata = Metadata;
        var nativeLibraries = NativeLibraries;

        return option.Permissions.All(permissions.Contains)
            && option.Features.All(features.Contains)
            && (option.ApplicationMetadata is null || option.ApplicationMetadata.All(pair =>
                metadata.TryGetValue(pair.Key, out var value) && value == pair.Value))
            && (option.NativeLibraries is null || option.NativeLibraries.All(nativeLibraries.Contains));
    }

    public void SetOption(ManifestOptionDefinition option, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (enabled)
        {
            foreach (var permission in option.Permissions) AddPermission(permission);
            foreach (var feature in option.Features) AddFeature(feature);
            if (option.ApplicationMetadata is not null)
            {
                foreach (var pair in option.ApplicationMetadata) SetMetadata(pair.Key, pair.Value);
            }

            if (option.NativeLibraries is not null)
            {
                foreach (var library in option.NativeLibraries) AddNativeLibrary(library);
            }
        }
        else
        {
            foreach (var permission in option.Permissions) RemovePermission(permission);
            foreach (var feature in option.Features) RemoveFeature(feature);
            if (option.ApplicationMetadata is not null)
            {
                foreach (var key in option.ApplicationMetadata.Keys) RemoveMetadata(key);
            }

            if (option.NativeLibraries is not null)
            {
                foreach (var library in option.NativeLibraries) RemoveNativeLibrary(library);
            }
        }
    }

    public void AddPermission(string permission) => AddNamedManifestChild("uses-permission", permission, required: null);

    public void RemovePermission(string permission) => RemoveNamedManifestChild("uses-permission", permission);

    public void AddFeature(string feature) => AddNamedManifestChild("uses-feature", feature, required: false);

    public void RemoveFeature(string feature) => RemoveNamedManifestChild("uses-feature", feature);

    public void AddNativeLibrary(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (_application.Elements("uses-native-library").Any(element => (string?)element.Attribute(Android + "name") == fileName))
        {
            return;
        }

        _application.Add(new XElement(
            "uses-native-library",
            new XAttribute(Android + "name", fileName),
            new XAttribute(Android + "required", "false")));
    }

    public void RemoveNativeLibrary(string fileName)
    {
        _application.Elements("uses-native-library")
            .Where(element => (string?)element.Attribute(Android + "name") == fileName)
            .Remove();
    }

    public void SetMetadata(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        var matches = _application.Elements("meta-data")
            .Where(element => (string?)element.Attribute(Android + "name") == name)
            .ToArray();
        var element = matches.FirstOrDefault();
        if (element is null)
        {
            element = new XElement("meta-data", new XAttribute(Android + "name", name));
            _application.Add(element);
        }

        element.SetAttributeValue(Android + "value", value);
        foreach (var duplicate in matches.Skip(1)) duplicate.Remove();
    }

    public void RemoveMetadata(string name)
    {
        _application.Elements("meta-data")
            .Where(element => (string?)element.Attribute(Android + "name") == name)
            .Remove();
    }

    private static (XDocument Document, XElement Manifest, XElement Application) Parse(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or InvalidOperationException)
        {
            throw new ArgumentException("The supplied text is not valid XML.", nameof(xml), exception);
        }

        var manifest = document.Root;
        if (manifest is null || manifest.Name.LocalName != "manifest")
        {
            throw new ArgumentException("The XML document has no Android <manifest> root element.", nameof(xml));
        }

        var androidNamespaceDeclared = manifest.Attributes().Any(attribute =>
            attribute.IsNamespaceDeclaration && attribute.Value == AndroidNamespaceUri);
        if (!androidNamespaceDeclared)
        {
            throw new ArgumentException("The manifest does not declare the Android XML namespace.", nameof(xml));
        }

        var application = manifest.Elements().FirstOrDefault(element => element.Name.LocalName == "application")
            ?? throw new ArgumentException("The manifest has no <application> element.", nameof(xml));

        return (document, manifest, application);
    }

    private string[] GetNamedManifestChildren(string localName) => _manifest.Elements()
        .Where(element => element.Name.LocalName == localName)
        .Select(element => (string?)element.Attribute(Android + "name"))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Cast<string>()
        .ToArray();

    private void AddNamedManifestChild(string localName, string name, bool? required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_manifest.Elements().Any(element =>
            element.Name.LocalName == localName && (string?)element.Attribute(Android + "name") == name))
        {
            return;
        }

        var element = new XElement(localName, new XAttribute(Android + "name", name));
        if (required is not null)
        {
            element.SetAttributeValue(Android + "required", required.Value ? "true" : "false");
        }

        _manifest.Add(element);
    }

    private void RemoveNamedManifestChild(string localName, string name)
    {
        _manifest.Elements()
            .Where(element => element.Name.LocalName == localName && (string?)element.Attribute(Android + "name") == name)
            .Remove();
    }

    private void RemoveDuplicateMetadata()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in _application.Elements("meta-data").ToArray())
        {
            var name = (string?)element.Attribute(Android + "name");
            var value = (string?)element.Attribute(Android + "value");
            if (name is null || value is null || !seen.Add(name))
            {
                element.Remove();
            }
        }
    }
}
