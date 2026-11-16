using System.Runtime.Serialization;

namespace Forge.Next.Formatters;

/// <summary>
/// A <see cref="SerializationBinder"/> for the <c>SoapFormatter</c> that lets a SOAP payload name only the types it was
/// given (since 3.4.0). Without a binder, <c>SoapFormatter.Deserialize</c> creates whatever types the payload names, so
/// untrusted input can make it create types the caller never expected; with this binder any other type is refused
/// with a <see cref="SerializationException"/> before it is created, which <see cref="XmlSoapFormatter{T}"/> returns as
/// an <c>Unexpected</c> error.
/// <para>
/// A type is matched by its full name; the binder then returns the allowed type itself, so a payload that names the
/// same type in another assembly or version still gets the allowed one. A generic type's full name contains its type
/// arguments with their assemblies, so it matches only a payload written with the same ones. List every type the
/// payload contains: the root type, the custom types of its fields, the element types of its arrays (an array
/// type itself does not reach the binder), and generic collections such as <c>List&lt;Line&gt;</c>. Primitive values
/// and strings do not reach the binder either.
/// </para>
/// <para>
/// On <c>net48</c> the SOAP formatter creates the types of <c>mscorlib</c> (for example <c>List&lt;T&gt;</c>,
/// <c>ArrayList</c>, <c>Version</c>) without asking the binder, so there it cannot refuse them; read only trusted SOAP
/// input on that target.
/// </para>
/// </summary>
public sealed class SoapAllowedTypesBinder : SerializationBinder
{

    private readonly Dictionary<string, Type> _allowedTypes = new Dictionary<string, Type>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="SoapAllowedTypesBinder"/> class.
    /// </summary>
    /// <param name="allowedTypes">The types a payload may name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="allowedTypes"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An element of <paramref name="allowedTypes"/> is <c>null</c>.</exception>
    public SoapAllowedTypesBinder(params Type[] allowedTypes)
    {
        if (allowedTypes is null) throw new ArgumentNullException(nameof(allowedTypes));

        foreach (Type type in allowedTypes)
        {
            if (type is null) throw new ArgumentException("An allowed type is null.", nameof(allowedTypes));

            _allowedTypes[type.FullName ?? type.Name] = type;
        }
    }

    /// <summary>
    /// Returns the allowed type that <paramref name="typeName"/> names, or refuses the type.
    /// </summary>
    /// <param name="assemblyName">The assembly the payload names; not used for the match.</param>
    /// <param name="typeName">The full name of the type the payload names.</param>
    /// <returns>The allowed type of that full name.</returns>
    /// <exception cref="SerializationException">The type is not allowed.</exception>
    public override Type BindToType(string assemblyName, string typeName)
    {
        if (typeName is not null && _allowedTypes.TryGetValue(typeName, out Type? type)) return type;

        throw new SerializationException($"The SOAP input names a type that is not allowed: '{typeName}' in '{assemblyName}'.");
    }

}
