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
    /// Creates a formatter that reads only payloads made of <typeparamref name="T"/> and
    /// <paramref name="otherAllowedTypes"/> (since 3.4.0): its <see cref="SoapFormatter"/> has a
    /// <see cref="SoapAllowedTypesBinder"/>, so a payload that names any other type gives an <c>Unexpected</c> error
    /// before that type is created. Use it for input that is not fully trusted; the parameterless constructor
    /// creates whatever types a payload names. On <c>net48</c> the types of <c>mscorlib</c> are created without asking
    /// the binder, so there it refuses only the types of other assemblies.
    /// </summary>
    /// <param name="otherAllowedTypes">The other types the payload may contain: the custom types of the fields of
    /// <typeparamref name="T"/>, the element types of its arrays (not the array types), and its generic collections
    /// such as <c>List&lt;Line&gt;</c>. Primitive values and strings need not be listed.</param>
    /// <returns>The new formatter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="otherAllowedTypes"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An element of <paramref name="otherAllowedTypes"/> is <c>null</c>.</exception>
    public static XmlSoapFormatter<T> WithAllowedTypes(params Type[] otherAllowedTypes)
    {
        if (otherAllowedTypes is null) throw new ArgumentNullException(nameof(otherAllowedTypes));

        Type[] allowedTypes = new Type[otherAllowedTypes.Length + 1];
        allowedTypes[0] = typeof(T);
        Array.Copy(otherAllowedTypes, 0, allowedTypes, 1, otherAllowedTypes.Length);

        return new XmlSoapFormatter<T>(new SoapFormatter { Binder = new SoapAllowedTypesBinder(allowedTypes) });
    }

    /// <summary>
    /// Reads and deserializes an object of type T from the provided input stream using the SoapFormatter.
    /// </summary>
    /// <param name="inputStream">The input stream containing the serialized object.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized object of type T.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancellation (since 3.4.0 thrown, not returned as an error).</exception>
    public Task<ErrorOr<T?>> ReadAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (inputStream is null) return Task.FromResult((ErrorOr<T?>)Error.Validation(description: nameof(inputStream)));

        return this.ProtectAsync<XmlSoapFormatter<T>, T?>(async (_, _) =>
        {
            // Checked once here: the synchronous serializer below cannot observe the token.
            cancellationToken.ThrowIfCancellationRequested();

            // SoapFormatter.Deserialize closes the stream it reads, so it gets a copy: the caller's stream stays
            // open, at its end, as after the other formatters.
            using MemoryStream copy = new MemoryStream();
            await inputStream.CopyToAsync(copy, Consts.DefaultBufferSize, cancellationToken).ConfigureAwait(false);
            copy.Position = 0;

            return (T?)_formatter.Deserialize(copy);
        }, rethrowCancellation: true, cancellationToken: cancellationToken);
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
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancellation (since 3.4.0 thrown, not returned as an error).</exception>
    public Task<ErrorOr<Success>> WriteAsync(T data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(data)));
        if (outputStream is null) return Task.FromResult((ErrorOr<Success>)Error.Validation(description: nameof(outputStream)));

        return this.ProtectAsync<XmlSoapFormatter<T>, Success>(async (_, _) =>
        {
            // Checked once here: the synchronous serializer below cannot observe the token.
            cancellationToken.ThrowIfCancellationRequested();

            _formatter.Serialize(outputStream, data);
            return Result.Success;
        }, rethrowCancellation: true, cancellationToken: cancellationToken);
    }

}
