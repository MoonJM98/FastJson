using Xunit;

namespace FastJson.IntegrationTests;

public class AsyncSerializationTests
{
    [Fact]
    public async Task SerializeAsync_SimpleObject_WritesToStream()
    {
        // Arrange
        var person = new Person { Name = "Alice", Age = 30 };
        using var stream = new MemoryStream();

        // Act
        await FastJson.SerializeAsync(stream, person);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"Alice\"", json);
    }

    [Fact]
    public async Task DeserializeAsync_ValidStream_ReturnsObject()
    {
        // Arrange
        var json = "{\"name\":\"Bob\",\"age\":25}";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        // Act
        var person = await FastJson.DeserializeAsync<Person>(stream);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("Bob", person.Name);
        Assert.Equal(25, person.Age);
    }

    [Fact]
    public async Task RoundTrip_Async_PreservesData()
    {
        // Arrange
        var original = new Order
        {
            OrderId = 456,
            Customer = new Person { Name = "Diana", Age = 35 },
            Items = new List<OrderItem>
            {
                new() { ProductName = "AsyncItem", Quantity = 3, Price = 15.0m }
            }
        };

        using var stream = new MemoryStream();

        // Act
        await FastJson.SerializeAsync(stream, original);
        stream.Position = 0;
        var deserialized = await FastJson.DeserializeAsync<Order>(stream);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.OrderId, deserialized.OrderId);
        Assert.Equal(original.Customer.Name, deserialized.Customer?.Name);
        Assert.Single(deserialized.Items!);
        Assert.Equal("AsyncItem", deserialized.Items![0].ProductName);
    }

    [Fact]
    public async Task SerializeAsync_List_WritesJsonArray()
    {
        // Arrange
        var people = new List<Person>
        {
            new() { Name = "Eve", Age = 28 },
            new() { Name = "Frank", Age = 32 }
        };
        using var stream = new MemoryStream();

        // Act
        await FastJson.SerializeAsync(stream, people);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        Assert.StartsWith("[", json);
        Assert.Contains("Eve", json);
        Assert.Contains("Frank", json);
    }

    [Fact]
    public async Task DeserializeAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var json = "{\"name\":\"Test\",\"age\":1}";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await FastJson.DeserializeAsync<Person>(stream, cts.Token));
    }
}
