namespace Forge.Next.Formatters;

/// <summary>
/// SOAP serialization with the <c>SoapFormatter</c> of the SoapFormatter package: an <see cref="IDataFormatter{T}"/>,
/// implemented by <see cref="XmlSoapFormatter{T}"/>.
/// </summary>
/// <typeparam name="T">The type of object to be serialized and deserialized.</typeparam>
public interface IXmlSoapFormatter<T> : IDataFormatter<T>
{
}
