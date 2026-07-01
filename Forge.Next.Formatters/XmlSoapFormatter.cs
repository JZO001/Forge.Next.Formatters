using ErrorOr;
using Forge.Next.Shared;
using System.Runtime.Serialization.Formatters.Soap;

namespace Forge.Next.Formatters;

/// <summary>
/// Implementation of the IXmlSoapFormatter interface using the SoapFormatter for XML serialization and deserialization.
/// </summary>
/// <typeparam name="T"></typeparam>
public class XmlSoapFormatter<T> : IXmlSoapFormatter<T>
{

    private readonly SoapFormatter _formatter = new SoapFormatter();

    /// <summary>
    /// Initializes a new instance of the XmlSoapFormatter class.
    /// </summary>
    public XmlSoapFormatter()
    {
    }

    /// <summary>
    /// Initializes a new instance of the XmlSoapFormatter class with the specified SoapFormatter.
    /// </summary>
    /// <param name="formatter">The SoapFormatter to use for serialization and deserialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided formatter is null.</exception>
    public XmlSoapFormatter(SoapFormatter formatter)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    /// <summary>
    /// Reads and deserializes an object of type T from the provided input stream using the SoapFormatter.
    /// </summary>
    /// <param name="inputStream">The input stream containing the serialized object.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized object of type T.</returns>
    public Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(inputStream)));

        return this.ProtectAsync<XmlSoapFormatter<T>, T?>(async (_, _) =>
        {
            return (T?)_formatter.Deserialize(inputStream);
        });
    }

    /// <summary>
    /// Reads and deserializes an object of type T from the provided input stream and writes it to the specified output stream using the SoapFormatter.
    /// </summary>
    /// <param name="inputStream">The input stream containing the serialized object.</param>
    /// <param name="outputStream">The output stream to write the deserialized object to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
    public Task<ErrorOr<Success>> ReadAsync(Stream inputStream, Stream outputStream, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((ErrorOr<Success>)Error.Forbidden(description: "Method not implemented."));
    }

    /// <summary>
    /// Serializes and writes an object of type T to the provided output stream using the SoapFormatter.
    /// </summary>
    /// <param name="data">The object of type T to be serialized and written.</param>
    /// <param name="outputStream">The output stream to write the serialized object to.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
    public Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));

        return this.ProtectAsync<XmlSoapFormatter<T>, Success>(async (_, _) =>
        {
            _formatter.Serialize(outputStream, data);
            return Result.Success;
        });
    }

}
