using ErrorOr;
using Forge.Next.Shared;
using System.Text;
using System.Xml.Serialization;

namespace Forge.Next.Formatters;

/// <summary>
/// Implementation of IXmlDataFormatter for serializing and deserializing XML data
/// </summary>
/// <typeparam name="T"></typeparam>
public class XmlDataFormatter<T> : IXmlDataFormatter<T>
{

    /// <summary>
    /// Gets or sets the encoding used for reading and writing XML data. Default is UTF-8.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Reads XML data from the provided input stream and deserializes it into an object of type T.
    /// </summary>
    /// <param name="inputStream">The input stream containing the XML data.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized object of type T.</returns>
    public Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(inputStream)));

        return this.ProtectAsync<XmlDataFormatter<T>, T?>(async (_, _) =>
        {
            using StreamReader sr = new StreamReader(inputStream, encoding: Encoding, leaveOpen: true);
            return (T?)new XmlSerializer(typeof(T)).Deserialize(sr);
        });
    }

    /// <summary>
    /// Reads XML data from the provided input stream and writes it to the specified output stream. This method is not implemented and will return a forbidden error.
    /// </summary>
    /// <param name="inputStream">The input stream containing the XML data.</param>
    /// <param name="outputStream">The output stream to write the XML data to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
    public Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((ErrorOr<Success>)Error.Forbidden(description: "Method not implemented."));
    }

    /// <summary>
    /// Writes the provided data of type T to the specified output stream in XML format.
    /// </summary>
    /// <param name="data">The data to be written to the output stream.</param>
    /// <param name="outputStream">The output stream to write the XML data to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
    public Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));

        return this.ProtectAsync<XmlDataFormatter<T>, Success>(async (_, _) =>
        {
            using StreamWriter sw = new StreamWriter(outputStream, encoding: Encoding, leaveOpen: true);

            new XmlSerializer(typeof(T)).Serialize(sw, data);
            await sw.FlushAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success;
        });
    }

}
