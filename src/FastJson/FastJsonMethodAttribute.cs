using System;

namespace FastJson;

/// <summary>
/// Marks a method as using FastJson internally for serialization/deserialization.
/// When a generic method with this attribute is called with concrete type arguments,
/// the FastJson source generator will automatically register those types.
/// </summary>
/// <example>
/// <code>
/// // In library project (FastNode.Realtime)
/// public class ServerSentEvent
/// {
///     [FastJsonMethod]
///     public static string CreateJson&lt;T&gt;(T data) =&gt; FastJson.Serialize(data);
/// }
///
/// // In consumer project (TestProject) - TestValueDto is automatically registered
/// var json = ServerSentEvent.CreateJson(new TestValueDto());
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FastJsonMethodAttribute : Attribute
{
}
