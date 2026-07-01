using System.Text;

namespace Forge.Next.Formatters;

/// <summary>
/// Interface for IXmlDataFormatter
/// </summary>
/// <typeparam name="T">The type of object to be serialized and deserialized.</typeparam>
public interface IXmlDataFormatter<T> : IDataFormatter<T>
{

    /// <summary>
    /// Gets or sets the encoding used for reading and writing XML data in the IXmlDataFormatter.
    /// </summary>
    Encoding Encoding { get; set; }

}
