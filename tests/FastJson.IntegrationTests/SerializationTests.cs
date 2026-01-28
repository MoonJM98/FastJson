using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xunit;

namespace FastJson.IntegrationTests;

public class SerializationTests
{
    [Fact]
    public void Serialize_SimpleObject_ReturnsValidJson()
    {
        // Arrange
        var person = new Person { Name = "Alice", Age = 30 };

        // Act
        var json = FastJson.Serialize(person);

        // Assert
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"Alice\"", json);
        Assert.Contains("\"age\"", json);
        Assert.Contains("30", json);
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsObject()
    {
        // Arrange
        var json = "{\"name\":\"Bob\",\"age\":25}";

        // Act
        var person = FastJson.Deserialize<Person>(json);

        // Assert
        Assert.NotNull(person);
        Assert.Equal("Bob", person.Name);
        Assert.Equal(25, person.Age);
    }

    [Fact]
    public void RoundTrip_ComplexObject_PreservesData()
    {
        // Arrange
        var original = new Order
        {
            OrderId = 123,
            Customer = new Person { Name = "Charlie", Age = 40 },
            Items = new List<OrderItem>
            {
                new() { ProductName = "Item1", Quantity = 2, Price = 10.5m },
                new() { ProductName = "Item2", Quantity = 1, Price = 20.0m }
            }
        };

        // Act
        var json = FastJson.Serialize(original);
        var deserialized = FastJson.Deserialize<Order>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.OrderId, deserialized.OrderId);
        Assert.NotNull(deserialized.Customer);
        Assert.Equal(original.Customer.Name, deserialized.Customer.Name);
        Assert.NotNull(deserialized.Items);
        Assert.Equal(2, deserialized.Items.Count);
        Assert.Equal("Item1", deserialized.Items[0].ProductName);
    }

    [Fact]
    public void Serialize_List_ReturnsJsonArray()
    {
        // Arrange
        var people = new List<Person>
        {
            new() { Name = "Alice", Age = 30 },
            new() { Name = "Bob", Age = 25 }
        };

        // Act
        var json = FastJson.Serialize(people);

        // Assert
        Assert.StartsWith("[", json);
        Assert.EndsWith("]", json);
        Assert.Contains("Alice", json);
        Assert.Contains("Bob", json);
    }

    [Fact]
    public void Deserialize_JsonArray_ReturnsList()
    {
        // Arrange
        var json = "[{\"name\":\"Alice\",\"age\":30},{\"name\":\"Bob\",\"age\":25}]";

        // Act
        var people = FastJson.Deserialize<List<Person>>(json);

        // Assert
        Assert.NotNull(people);
        Assert.Equal(2, people.Count);
        Assert.Equal("Alice", people[0].Name);
        Assert.Equal("Bob", people[1].Name);
    }

    [Fact]
    public void Serialize_NullValue_ReturnsNullJson()
    {
        // Arrange
        Person? person = null;

        // Act
        var json = FastJson.Serialize(person);

        // Assert
        Assert.Equal("null", json);
    }

    [Fact]
    public void Deserialize_NullJson_ReturnsNull()
    {
        // Arrange
        var json = "null";

        // Act
        var person = FastJson.Deserialize<Person>(json);

        // Assert
        Assert.Null(person);
    }

    [Fact]
    public void Serialize_Dictionary_ReturnsJsonObject()
    {
        // Arrange
        var dict = new Dictionary<string, int>
        {
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3
        };

        // Act
        var json = FastJson.Serialize(dict);

        // Assert
        Assert.Contains("\"one\"", json);
        Assert.Contains("\"two\"", json);
        Assert.Contains("\"three\"", json);
    }

    [Fact]
    public void RoundTrip_NestedCollections_PreservesStructure()
    {
        // Arrange
        var data = new DataContainer
        {
            Nested = new List<List<int>>
            {
                new() { 1, 2, 3 },
                new() { 4, 5, 6 }
            }
        };

        // Act
        var json = FastJson.Serialize(data);
        var result = FastJson.Deserialize<DataContainer>(json);

        // Assert
        Assert.NotNull(result?.Nested);
        Assert.Equal(2, result.Nested.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.Nested[0]);
        Assert.Equal(new[] { 4, 5, 6 }, result.Nested[1]);
    }

    [Fact]
    public void RoundTrip_UserDefinedCollection_PreservesData()
    {
        // Arrange - uses CustomCollection<TElement> (not <T>)
        var container = new CustomCollectionContainer
        {
            People = new CustomCollection<Person>
            {
                new() { Name = "Alice", Age = 30 },
                new() { Name = "Bob", Age = 25 }
            }
        };

        // Act
        var json = FastJson.Serialize(container);
        var result = FastJson.Deserialize<CustomCollectionContainer>(json);

        // Assert
        Assert.NotNull(result?.People);
        Assert.Equal(2, result.People.Count);
        Assert.Equal("Alice", result.People[0].Name);
        Assert.Equal("Bob", result.People[1].Name);
    }

    [Fact]
    public void Serialize_WithJsonPropertyName_UsesCustomName()
    {
        // Arrange
        var product = new ProductWithCustomName
        {
            Id = 123,
            Name = "Widget",
            Price = 9.99m
        };

        // Act
        var json = FastJson.Serialize(product);

        // Assert
        Assert.Contains("\"product_id\"", json);
        Assert.Contains("\"product_name\"", json);
        Assert.Contains("\"price\"", json); // Default camelCase
        Assert.DoesNotContain("\"id\"", json);
        Assert.DoesNotContain("\"name\"", json);
    }

    [Fact]
    public void Deserialize_WithJsonPropertyName_ReadsCustomName()
    {
        // Arrange
        var json = "{\"product_id\":456,\"product_name\":\"Gadget\",\"price\":19.99}";

        // Act
        var product = FastJson.Deserialize<ProductWithCustomName>(json);

        // Assert
        Assert.NotNull(product);
        Assert.Equal(456, product.Id);
        Assert.Equal("Gadget", product.Name);
        Assert.Equal(19.99m, product.Price);
    }

    [Fact]
    public void Serialize_WithJsonIgnore_ExcludesProperty()
    {
        // Arrange
        var user = new UserWithSecret
        {
            Username = "john_doe",
            Password = "secret123",
            Email = "john@example.com"
        };

        // Act
        var json = FastJson.Serialize(user);

        // Assert
        Assert.Contains("\"username\"", json);
        Assert.Contains("\"email\"", json);
        Assert.DoesNotContain("password", json.ToLower());
        Assert.DoesNotContain("secret123", json);
    }

    [Fact]
    public void RoundTrip_Record_PreservesData()
    {
        // Arrange
        var person = new PersonRecord("Alice", 30);

        // Act
        var json = FastJson.Serialize(person);
        var result = FastJson.Deserialize<PersonRecord>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Alice", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void RoundTrip_RecordWithJsonPropertyName_PreservesData()
    {
        // Arrange
        var product = new ProductRecord(123, "Widget", 9.99m);

        // Act
        var json = FastJson.Serialize(product);
        var result = FastJson.Deserialize<ProductRecord>(json);

        // Assert
        Assert.Contains("\"product_id\"", json);
        Assert.Contains("\"product_name\"", json);
        Assert.NotNull(result);
        Assert.Equal(123, result.Id);
        Assert.Equal("Widget", result.Name);
        Assert.Equal(9.99m, result.Price);
    }

    [Fact]
    public void RoundTrip_ImmutableWithJsonConstructor_PreservesData()
    {
        // Arrange
        var point = new ImmutablePoint(10, 20);

        // Act
        var json = FastJson.Serialize(point);
        var result = FastJson.Deserialize<ImmutablePoint>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
    }

    [Fact]
    public void RoundTrip_ClassWithJsonIncludeField_IncludesField()
    {
        // Arrange
        var obj = new ClassWithJsonIncludeField
        {
            Name = "Test",
            _internalId = 42
        };

        // Act
        var json = FastJson.Serialize(obj);
        var result = FastJson.Deserialize<ClassWithJsonIncludeField>(json);

        // Assert
        Assert.Contains("internalId", json);
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(42, result._internalId);
    }

    [Fact]
    public void Serialize_EnumAsNumber_ReturnsNumber()
    {
        // Arrange
        var order = new OrderWithStatus
        {
            OrderId = 1,
            Status = OrderStatus.Shipped,
            Priority = Priority.High
        };

        // Act
        var json = FastJson.Serialize(order);

        // Assert
        Assert.Contains("\"status\":2", json); // Enum as number
        Assert.Contains("\"High\"", json);     // Priority with JsonStringEnumConverter
    }

    [Fact]
    public void RoundTrip_EnumWithStringConverter_PreservesValue()
    {
        // Arrange
        var order = new OrderWithStatus
        {
            OrderId = 1,
            Status = OrderStatus.Processing,
            Priority = Priority.Critical
        };

        // Act
        var json = FastJson.Serialize(order);
        var result = FastJson.Deserialize<OrderWithStatus>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Processing, result.Status);
        Assert.Equal(Priority.Critical, result.Priority);
    }

    [Fact]
    public void Deserialize_NumberFromString_WithJsonNumberHandling()
    {
        // Arrange
        var json = "{\"name\":\"Alice\",\"age\":\"30\"}"; // age as string

        // Act
        var result = FastJson.Deserialize<ClassWithNumberHandling>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Alice", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void Serialize_PolymorphicType_IncludesTypeDiscriminator()
    {
        // Arrange
        Animal dog = new Dog { Name = "Buddy", Breed = "Golden Retriever" };

        // Act
        var json = FastJson.Serialize(dog);

        // Assert
        Assert.Contains("\"$type\":\"dog\"", json);
        Assert.Contains("\"name\":\"Buddy\"", json);
        Assert.Contains("\"breed\":\"Golden Retriever\"", json);
    }

    [Fact]
    public void Deserialize_PolymorphicType_RestoresCorrectType()
    {
        // Arrange
        var json = "{\"$type\":\"cat\",\"name\":\"Whiskers\",\"isIndoor\":true}";

        // Act
        var animal = FastJson.Deserialize<Animal>(json);

        // Assert
        Assert.NotNull(animal);
        Assert.IsType<Cat>(animal);
        var cat = (Cat)animal;
        Assert.Equal("Whiskers", cat.Name);
        Assert.True(cat.IsIndoor);
    }

    [Fact]
    public void RoundTrip_PolymorphicList_PreservesTypes()
    {
        // Arrange
        var zoo = new Zoo
        {
            ZooName = "City Zoo",
            Animals = new List<Animal>
            {
                new Dog { Name = "Rex", Breed = "German Shepherd" },
                new Cat { Name = "Mittens", IsIndoor = false }
            }
        };

        // Act
        var json = FastJson.Serialize(zoo);
        var result = FastJson.Deserialize<Zoo>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("City Zoo", result.ZooName);
        Assert.NotNull(result.Animals);
        Assert.Equal(2, result.Animals.Count);
        Assert.IsType<Dog>(result.Animals[0]);
        Assert.IsType<Cat>(result.Animals[1]);
        Assert.Equal("Rex", result.Animals[0].Name);
        Assert.Equal("German Shepherd", ((Dog)result.Animals[0]).Breed);
        Assert.Equal("Mittens", result.Animals[1].Name);
        Assert.False(((Cat)result.Animals[1]).IsIndoor);
    }

    // ============ New Feature Tests ============

    [Fact]
    public void SerializeToUtf8Bytes_SimpleObject_ReturnsBytes()
    {
        // Arrange
        var person = new Person { Name = "Alice", Age = 30 };

        // Act
        var bytes = FastJson.SerializeToUtf8Bytes(person);

        // Assert
        Assert.NotNull(bytes);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"Alice\"", json);
        Assert.Contains("\"age\"", json);
        Assert.Contains("30", json);
    }

    [Fact]
    public void Deserialize_ToJsonNode_ReturnsValidNode()
    {
        // Arrange
        var json = "{\"name\":\"Alice\",\"age\":30}";

        // Act
        var node = FastJson.Deserialize(json);

        // Assert
        Assert.NotNull(node);
        Assert.Equal("Alice", node["name"]?.GetValue<string>());
        Assert.Equal(30, node["age"]?.GetValue<int>());
    }

    [Fact]
    public void Deserialize_Utf8Bytes_ToJsonNode_ReturnsValidNode()
    {
        // Arrange
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"name\":\"Bob\",\"value\":42}");

        // Act
        var node = FastJson.Deserialize(bytes);

        // Assert
        Assert.NotNull(node);
        Assert.Equal("Bob", node["name"]?.GetValue<string>());
        Assert.Equal(42, node["value"]?.GetValue<int>());
    }

    [Fact]
    public void Serialize_SnakeCaseModel_UsesSnakeCaseNames()
    {
        // Arrange
        var model = new SnakeCaseModel
        {
            UserName = "john_doe",
            UserId = 123,
            EmailAddress = "john@example.com"
        };

        // Act
        var json = FastJson.Serialize(model);

        // Assert
        Assert.Contains("\"user_name\"", json);
        Assert.Contains("\"user_id\"", json);
        Assert.Contains("\"email_address\"", json);
        Assert.DoesNotContain("\"userName\"", json);
        Assert.DoesNotContain("\"userId\"", json);
    }

    [Fact]
    public void Serialize_KebabCaseModel_UsesKebabCaseNames()
    {
        // Arrange
        var model = new KebabCaseModel
        {
            FirstName = "John",
            LastName = "Doe"
        };

        // Act
        var json = FastJson.Serialize(model);

        // Assert
        Assert.Contains("\"first-name\"", json);
        Assert.Contains("\"last-name\"", json);
        Assert.DoesNotContain("\"firstName\"", json);
        Assert.DoesNotContain("\"lastName\"", json);
    }

    [Fact]
    public void RoundTrip_SnakeCaseModel_PreservesData()
    {
        // Arrange
        var original = new SnakeCaseModel
        {
            UserName = "alice",
            UserId = 456,
            EmailAddress = "alice@example.com"
        };

        // Act
        var json = FastJson.Serialize(original);
        var result = FastJson.Deserialize<SnakeCaseModel>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("alice", result.UserName);
        Assert.Equal(456, result.UserId);
        Assert.Equal("alice@example.com", result.EmailAddress);
    }

    [Fact]
    public void Deserialize_IgnoreCaseModel_MatchesDifferentCases()
    {
        // Arrange - use uppercase property names
        var json = "{\"USERNAME\":\"Alice\",\"AGE\":30}";

        // Act
        var result = FastJson.Deserialize<IgnoreCaseModel>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Alice", result.UserName);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void Deserialize_FlexibleModel_MatchesWithSpecialCharacters()
    {
        // Arrange - use snake_case and kebab-case in JSON
        var json = "{\"user_name\":\"Bob\",\"user-id\":42}";

        // Act
        var result = FastJson.Deserialize<FlexibleModel>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Bob", result.UserName);
        Assert.Equal(42, result.UserId);
    }

    [Fact]
    public void RoundTrip_DynamicModel_PreservesJsonNode()
    {
        // Arrange
        var model = new DynamicModel
        {
            Name = "Test",
            Extra = JsonNode.Parse("{\"foo\":\"bar\",\"count\":123}")
        };

        // Act
        var json = FastJson.Serialize(model);
        var result = FastJson.Deserialize<DynamicModel>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.NotNull(result.Extra);
        Assert.Equal("bar", result.Extra["foo"]?.GetValue<string>());
        Assert.Equal(123, result.Extra["count"]?.GetValue<int>());
    }

    [Fact]
    public void Serialize_DynamicModel_WithNullJsonNode_HandlesNull()
    {
        // Arrange
        var model = new DynamicModel
        {
            Name = "Test",
            Extra = null
        };

        // Act
        var json = FastJson.Serialize(model);

        // Assert
        Assert.Contains("\"name\":\"Test\"", json);
        // Extra should not appear when null (nullable reference type)
    }
}

// Test models
public class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

public class Order
{
    public int OrderId { get; set; }
    public Person? Customer { get; set; }
    public List<OrderItem>? Items { get; set; }
}

public class OrderItem
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class DataContainer
{
    public List<List<int>>? Nested { get; set; }
}

// User-defined collection with custom type parameter name
public class CustomCollection<TElement> : List<TElement>
{
}

// Container using user-defined collection
public class CustomCollectionContainer
{
    public CustomCollection<Person>? People { get; set; }
}

// Test model with [JsonPropertyName]
public class ProductWithCustomName
{
    [JsonPropertyName("product_id")]
    public int Id { get; set; }

    [JsonPropertyName("product_name")]
    public string Name { get; set; } = "";

    public decimal Price { get; set; }
}

// Test model with [JsonIgnore]
public class UserWithSecret
{
    public string Username { get; set; } = "";

    [JsonIgnore]
    public string Password { get; set; } = "";

    public string Email { get; set; } = "";
}

// Record type for constructor-based deserialization
public record PersonRecord(string Name, int Age);

// Record with [JsonPropertyName]
public record ProductRecord(
    [property: JsonPropertyName("product_id")] int Id,
    [property: JsonPropertyName("product_name")] string Name,
    decimal Price);

// Class with [JsonConstructor]
public class ImmutablePoint
{
    public int X { get; }
    public int Y { get; }

    [JsonConstructor]
    public ImmutablePoint(int x, int y)
    {
        X = x;
        Y = y;
    }
}

// Class with [JsonInclude] on private property
public class ClassWithPrivateProperty
{
    public string PublicName { get; set; } = "";

    [JsonInclude]
    private string _secretCode { get; set; } = "";

    public void SetSecretCode(string code) => _secretCode = code;
    public string GetSecretCode() => _secretCode;
}

// Class with [JsonInclude] on field
public class ClassWithJsonIncludeField
{
    public string Name { get; set; } = "";

    [JsonInclude]
    public int _internalId = 0;
}

// Enum for testing
public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Delivered = 3
}

// Enum with [JsonStringEnumConverter]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

// Class with enum properties
public class OrderWithStatus
{
    public int OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public Priority Priority { get; set; }
}

// Class with [JsonNumberHandling]
public class ClassWithNumberHandling
{
    public string Name { get; set; } = "";

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Age { get; set; }
}

// Polymorphic base class
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Dog), "dog")]
[JsonDerivedType(typeof(Cat), "cat")]
public abstract class Animal
{
    public string Name { get; set; } = "";
}

// Derived type: Dog
public class Dog : Animal
{
    public string Breed { get; set; } = "";
}

// Derived type: Cat
public class Cat : Animal
{
    public bool IsIndoor { get; set; }
}

// Container with polymorphic property
public class Zoo
{
    public string ZooName { get; set; } = "";
    public List<Animal>? Animals { get; set; }
}

// Test model with [JsonNaming] - SnakeCase
[JsonNaming(NamingPolicy.SnakeCase)]
public class SnakeCaseModel
{
    public string UserName { get; set; } = "";
    public int UserId { get; set; }
    public string EmailAddress { get; set; } = "";
}

// Test model with [JsonNaming] - KebabCase
[JsonNaming(NamingPolicy.KebabCase)]
public class KebabCaseModel
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

// Test model with [JsonNaming] - IgnoreCase
[JsonNaming(NamingPolicy.CamelCase, IgnoreCase = true)]
public class IgnoreCaseModel
{
    public string UserName { get; set; } = "";
    public int Age { get; set; }
}

// Test model with [JsonNaming] - IgnoreSpecialCharacters
[JsonNaming(NamingPolicy.CamelCase, IgnoreSpecialCharacters = true)]
public class FlexibleModel
{
    public string UserName { get; set; } = "";
    public int UserId { get; set; }
}

// Test model with JsonNode property
public class DynamicModel
{
    public string Name { get; set; } = "";
    public JsonNode? Extra { get; set; }
}
