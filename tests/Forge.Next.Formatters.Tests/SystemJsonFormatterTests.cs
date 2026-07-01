using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ErrorOr;
using Forge.Next.Formatters;
using Xunit;

namespace Forge.Next.Formatters.Tests;

/// <summary>
/// A simple, JSON-serializable data-transfer object used by <see cref="SystemJsonFormatterTests"/>.
/// System.Text.Json works with public types that expose public read/write properties.
/// </summary>
public class JsonSampleData
{
    /// <summary>A sample string property (PascalCase so naming-policy behaviour is observable).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A sample integer property.</summary>
    public int Value { get; set; }
}

/// <summary>
/// Unit tests for <see cref="SystemJsonFormatter{T}"/>, which serializes objects to JSON and back
/// using <see cref="System.Text.Json.JsonSerializer"/>. As with the other serialization formatters,
/// the two-stream read overload is intentionally unimplemented and returns a "forbidden" error.
/// </summary>
public class SystemJsonFormatterTests
{
    /// <summary>
    /// The default serializer options must be the framework's shared, read-only default instance.
    /// </summary>
    [Fact]
    public void SerializerOptions_Default_IsJsonSerializerOptionsDefault_Test()
    {
        // Act
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>();

        // Assert: the property points at the framework's shared default options instance.
        Assert.Same(JsonSerializerOptions.Default, formatter.SerializerOptions);
    }

    /// <summary>
    /// The serializer options property must be assignable (round-trips the value).
    /// </summary>
    [Fact]
    public void SerializerOptions_Set_StoresValue_Test()
    {
        // Arrange
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>();
        JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };

        // Act
        formatter.SerializerOptions = options;

        // Assert
        Assert.Same(options, formatter.SerializerOptions);
    }

    /// <summary>
    /// A null data object on write yields a validation error naming "data".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullData_ReturnsValidationError_Test()
    {
        // Arrange
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>();
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(null!, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("data", result.FirstError.Description);
    }

    /// <summary>
    /// A null output stream on write yields a validation error naming "outputStream".
    /// </summary>
    [Fact]
    public async Task WriteAsync_NullOutputStream_ReturnsValidationError_Test()
    {
        // Arrange
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>();

        // Act
        ErrorOr<Success> result = await formatter.WriteAsync(new JsonSampleData(), null!);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Equal("outputStream", result.FirstError.Description);
    }

    /// <summary>
    /// The single-stream read overload does not guard its input explicitly; a null stream makes
    /// <c>JsonSerializer.DeserializeAsync</c> throw, which the <c>ProtectAsync</c> wrapper converts
    /// into an errored result. We therefore only assert that the operation reports an error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_NullInputStream_ReturnsError_Test()
    {
        // Arrange
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>();

        // Act
        ErrorOr<JsonSampleData?> result = await formatter.ReadAsync((Stream)null!);

        // Assert
        Assert.True(result.IsError);
    }

    /// <summary>
    /// The two-stream read overload is not implemented and must always return a "forbidden"
    /// error with the documented description.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WithOutputStream_ReturnsForbidden_Test()
    {
        // Arrange
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>();
        using MemoryStream input = new MemoryStream();
        using MemoryStream output = new MemoryStream();

        // Act
        ErrorOr<Success> result = await formatter.ReadAsync(input, output);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal("Method not implemented.", result.FirstError.Description);
    }

    /// <summary>
    /// Full round-trip: an object serialized with <c>WriteAsync</c> must deserialize back into an
    /// equal object with <c>ReadAsync</c>.
    /// </summary>
    [Fact]
    public async Task WriteAsyncThenReadAsync_RoundTripsObject_Test()
    {
        // Arrange
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>();
        JsonSampleData original = new JsonSampleData { Name = "Forge", Value = 99 };
        using MemoryStream buffer = new MemoryStream();

        // Act (write): serialize to JSON.
        ErrorOr<Success> writeResult = await formatter.WriteAsync(original, buffer);
        Assert.False(writeResult.IsError);

        // Act (read): rewind and deserialize.
        buffer.Position = 0;
        ErrorOr<JsonSampleData?> readResult = await formatter.ReadAsync(buffer);

        // Assert: the deserialized object has the same values.
        Assert.False(readResult.IsError);
        Assert.NotNull(readResult.Value);
        Assert.Equal(original.Name, readResult.Value!.Name);
        Assert.Equal(original.Value, readResult.Value!.Value);
    }

    /// <summary>
    /// The configured <see cref="JsonSerializerOptions"/> must actually be used while serializing.
    /// A camelCase naming policy is applied and the emitted JSON is checked for the camelCased
    /// property name ("name") instead of the CLR property name ("Name").
    /// </summary>
    [Fact]
    public async Task WriteAsync_UsesConfiguredSerializerOptions_Test()
    {
        // Arrange: a camelCase naming policy is a clearly observable option.
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>
        {
            SerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        };
        using MemoryStream buffer = new MemoryStream();

        // Act
        ErrorOr<Success> writeResult = await formatter.WriteAsync(new JsonSampleData { Name = "x", Value = 1 }, buffer);

        // Assert: success and the JSON uses the camelCased property name.
        Assert.False(writeResult.IsError);
        string json = Encoding.UTF8.GetString(buffer.ToArray());
        Assert.Contains("\"name\"", json);
        Assert.DoesNotContain("\"Name\"", json);
    }

    /// <summary>
    /// Feeding non-JSON content to <c>ReadAsync</c> causes the deserializer to throw; the
    /// <c>ProtectAsync</c> wrapper must convert that into an error result.
    /// </summary>
    [Fact]
    public async Task ReadAsync_InvalidJson_ReturnsError_Test()
    {
        // Arrange: bytes that are not valid JSON.
        SystemJsonFormatter<JsonSampleData> formatter = new SystemJsonFormatter<JsonSampleData>();
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("this is not json"));

        // Act
        ErrorOr<JsonSampleData?> result = await formatter.ReadAsync(input);

        // Assert
        Assert.True(result.IsError);
    }
}
