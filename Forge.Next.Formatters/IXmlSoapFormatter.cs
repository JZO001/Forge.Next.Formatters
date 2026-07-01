namespace Forge.Next.Formatters;

/// <summary>
/// Interface for IXmlSoapFormatter
/// </summary>
/// <typeparam name="T">The type of object to be serialized and deserialized.</typeparam>
public interface IXmlSoapFormatter<T> : IDataFormatter<T>
{
}
