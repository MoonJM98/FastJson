using FastJson;

// Sample types
var user = new User
{
    Id = 1,
    Name = "John Doe",
    Email = "john@example.com",
    CreatedAt = DateTime.UtcNow
};

var order = new Order
{
    OrderId = 12345,
    User = user,
    Items = new List<OrderItem>
    {
        new() { ProductName = "Widget", Quantity = 2, Price = 19.99m },
        new() { ProductName = "Gadget", Quantity = 1, Price = 49.99m }
    },
    OrderDate = DateTime.UtcNow
};

// Serialize
Console.WriteLine("=== Serialization ===");
string userJson = FastJson.FastJson.Serialize(user);
Console.WriteLine($"User JSON: {userJson}");

string orderJson = FastJson.FastJson.Serialize(order);
Console.WriteLine($"Order JSON: {orderJson}");

// Deserialize
Console.WriteLine("\n=== Deserialization ===");
var user2 = FastJson.FastJson.Deserialize<User>(userJson);
Console.WriteLine($"Deserialized User: Id={user2?.Id}, Name={user2?.Name}");

var order2 = FastJson.FastJson.Deserialize<Order>(orderJson);
Console.WriteLine($"Deserialized Order: OrderId={order2?.OrderId}, Items={order2?.Items?.Count}");

// List serialization
Console.WriteLine("\n=== List Serialization ===");
var users = new List<User> { user, new User { Id = 2, Name = "Jane Doe", Email = "jane@example.com" } };
string usersJson = FastJson.FastJson.Serialize(users);
Console.WriteLine($"Users JSON: {usersJson}");

var users2 = FastJson.FastJson.Deserialize<List<User>>(usersJson);
Console.WriteLine($"Deserialized Users Count: {users2?.Count}");

Console.WriteLine("\n=== Done ===");

// Type definitions
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class Order
{
    public int OrderId { get; set; }
    public User? User { get; set; }
    public List<OrderItem>? Items { get; set; }
    public DateTime OrderDate { get; set; }
}

public class OrderItem
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
