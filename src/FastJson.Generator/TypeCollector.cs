using System;
using System.Collections.Generic;
using System.Linq;
using FastJson.Generator.Models;
using Microsoft.CodeAnalysis;

namespace FastJson.Generator;

/// <summary>
/// Collects all types that need to be registered for JSON serialization.
/// Uses breadth-first search (BFS) to recursively collect nested types from properties,
/// generic arguments, collection elements, and polymorphic derived types.
/// </summary>
/// <remarks>
/// <para>
/// The collector starts with root types from FastJson.Serialize/Deserialize calls
/// and [FastJsonInclude] attributes, then expands to include:
/// </para>
/// <list type="bullet">
///   <item>Generic type arguments (e.g., T in List&lt;T&gt;)</item>
///   <item>Array element types</item>
///   <item>Public property types</item>
///   <item>Properties from base classes (for inheritance)</item>
///   <item>Derived types specified by [JsonDerivedType] attributes</item>
/// </list>
/// <para>
/// Primitive types (int, string, DateTime, etc.) are skipped as they are
/// handled natively by System.Text.Json.
/// </para>
/// </remarks>
public static class TypeCollector
{
    /// <summary>
    /// Set of fully qualified names for types that are handled natively by System.Text.Json.
    /// These types don't need custom JsonTypeInfo generation.
    /// </summary>
    private static readonly HashSet<string> PrimitiveTypeNames = new()
    {
        "System.Boolean",
        "System.Byte",
        "System.SByte",
        "System.Int16",
        "System.UInt16",
        "System.Int32",
        "System.UInt32",
        "System.Int64",
        "System.UInt64",
        "System.Single",
        "System.Double",
        "System.Decimal",
        "System.Char",
        "System.String",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.DateOnly",
        "System.TimeOnly",
        "System.TimeSpan",
        "System.Guid",
        "System.Uri",
        "System.Version",
        "System.Object",
        "System.Type"
    };


    /// <summary>
    /// Collects all types from the given root types, including nested property types and generic arguments.
    /// Returns a result with diagnostic information.
    /// </summary>
    public static TypeCollectionResult CollectAllTypesWithDiagnostics(IEnumerable<ITypeSymbol> rootTypes)
    {
        var visited = new HashSet<string>();
        var result = new List<TypeModel>();
        var queue = new Queue<(ITypeSymbol Type, int Depth)>();

        bool typeCountExceeded = false;
        bool depthExceeded = false;
        string? depthExceededTypeName = null;
        int maxDepthReached = 0;

        foreach (var type in rootTypes)
        {
            if (type != null)
            {
                queue.Enqueue((type, 0));
            }
        }

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            var fqn = GetFullyQualifiedName(current);

            if (visited.Contains(fqn))
                continue;

            if (IsPrimitiveType(current))
                continue;

            if (current.TypeKind == TypeKind.TypeParameter)
                continue;

            if (current.TypeKind == TypeKind.Error)
                continue;

            // Skip anonymous types (e.g., new { type = ..., error = ... })
            if (current is INamedTypeSymbol { IsAnonymousType: true })
                continue;

            // Check depth limit
            if (depth > TypeCollectionResult.MaxDepth)
            {
                if (!depthExceeded)
                {
                    depthExceeded = true;
                    depthExceededTypeName = fqn;
                    maxDepthReached = depth;
                }
                continue;
            }

            // Check type count limit
            if (result.Count >= TypeCollectionResult.MaxTypeCount)
            {
                typeCountExceeded = true;
                break;
            }

            visited.Add(fqn);

            var model = CreateTypeModel(current);
            result.Add(model);

            int nextDepth = depth + 1;

            // Collect generic type arguments
            if (current is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    if (!visited.Contains(GetFullyQualifiedName(typeArg)))
                    {
                        queue.Enqueue((typeArg, nextDepth));
                    }
                }
            }

            // Collect array element type
            if (current is IArrayTypeSymbol arrayType)
            {
                if (!visited.Contains(GetFullyQualifiedName(arrayType.ElementType)))
                {
                    queue.Enqueue((arrayType.ElementType, nextDepth));
                }
            }

            // Collect property types (for non-collection types)
            if (!IsCollectionType(current) && current.TypeKind != TypeKind.Array)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member is IPropertySymbol property &&
                        property.DeclaredAccessibility == Accessibility.Public &&
                        !property.IsStatic &&
                        !property.IsIndexer)
                    {
                        var propTypeFqn = GetFullyQualifiedName(property.Type);
                        if (!visited.Contains(propTypeFqn))
                        {
                            queue.Enqueue((property.Type, nextDepth));
                        }
                    }
                }
            }

            // Collect derived types for polymorphic types
            if (model.IsPolymorphic)
            {
                foreach (var derivedType in model.DerivedTypes)
                {
                    if (!visited.Contains(derivedType.TypeFullyQualifiedName))
                    {
                        // Find the derived type symbol from attributes
                        foreach (var attr in current.GetAttributes())
                        {
                            var attrClass = attr.AttributeClass;
                            if (attrClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonDerivedTypeAttribute")
                            {
                                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol derivedTypeSymbol)
                                {
                                    var derivedFqn = GetFullyQualifiedName(derivedTypeSymbol);
                                    if (derivedFqn == derivedType.TypeFullyQualifiedName && !visited.Contains(derivedFqn))
                                    {
                                        queue.Enqueue((derivedTypeSymbol, nextDepth));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return new TypeCollectionResult(
            result.ToEquatableArray(),
            typeCountExceeded,
            result.Count,
            depthExceeded,
            depthExceededTypeName,
            maxDepthReached);
    }

    /// <summary>
    /// Collects all types from the given root types, including nested property types and generic arguments.
    /// </summary>
    public static EquatableArray<TypeModel> CollectAllTypes(IEnumerable<ITypeSymbol> rootTypes)
    {
        var visited = new HashSet<string>();
        var result = new List<TypeModel>();
        var queue = new Queue<ITypeSymbol>();

        foreach (var type in rootTypes)
        {
            if (type != null)
            {
                queue.Enqueue(type);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var fqn = GetFullyQualifiedName(current);

            if (visited.Contains(fqn))
                continue;

            if (IsPrimitiveType(current))
                continue;

            if (current.TypeKind == TypeKind.TypeParameter)
                continue;

            if (current.TypeKind == TypeKind.Error)
                continue;

            // Skip anonymous types (e.g., new { type = ..., error = ... })
            if (current is INamedTypeSymbol { IsAnonymousType: true })
                continue;

            visited.Add(fqn);

            var model = CreateTypeModel(current);
            result.Add(model);

            // Collect generic type arguments
            if (current is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    if (!visited.Contains(GetFullyQualifiedName(typeArg)))
                    {
                        queue.Enqueue(typeArg);
                    }
                }
            }

            // Collect array element type
            if (current is IArrayTypeSymbol arrayType)
            {
                if (!visited.Contains(GetFullyQualifiedName(arrayType.ElementType)))
                {
                    queue.Enqueue(arrayType.ElementType);
                }
            }

            // Collect property types (for non-collection types)
            if (!IsCollectionType(current) && current.TypeKind != TypeKind.Array)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member is IPropertySymbol property &&
                        property.DeclaredAccessibility == Accessibility.Public &&
                        !property.IsStatic &&
                        !property.IsIndexer)
                    {
                        var propTypeFqn = GetFullyQualifiedName(property.Type);
                        if (!visited.Contains(propTypeFqn))
                        {
                            queue.Enqueue(property.Type);
                        }
                    }
                }
            }

            // Collect derived types for polymorphic types
            if (model.IsPolymorphic)
            {
                foreach (var derivedType in model.DerivedTypes)
                {
                    if (!visited.Contains(derivedType.TypeFullyQualifiedName))
                    {
                        // Find the derived type symbol from attributes
                        foreach (var attr in current.GetAttributes())
                        {
                            var attrClass = attr.AttributeClass;
                            if (attrClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonDerivedTypeAttribute")
                            {
                                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol derivedTypeSymbol)
                                {
                                    var derivedFqn = GetFullyQualifiedName(derivedTypeSymbol);
                                    if (derivedFqn == derivedType.TypeFullyQualifiedName && !visited.Contains(derivedFqn))
                                    {
                                        queue.Enqueue(derivedTypeSymbol);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return result.ToEquatableArray();
    }

    /// <summary>
    /// Creates a TypeModel from an ITypeSymbol.
    /// </summary>
    public static TypeModel CreateTypeModel(ITypeSymbol type)
    {
        var fqn = GetFullyQualifiedName(type);
        var typeName = GetTypeName(type);
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        var isGeneric = type is INamedTypeSymbol { IsGenericType: true };
        var isCollection = IsCollectionType(type) || type.TypeKind == TypeKind.Array;
        var isValueType = type.IsValueType;

        // Check for parameterless constructor
        var hasParameterlessConstructor = HasPublicParameterlessConstructor(type);

        // Collect properties
        var properties = CollectProperties(type);

        // Get element/key/value types for collections
        string? elementTypeName = null;
        string? keyTypeName = null;
        string? valueTypeName = null;

        if (type is IArrayTypeSymbol arrayType)
        {
            elementTypeName = GetFullyQualifiedName(arrayType.ElementType);
        }
        else if (type is INamedTypeSymbol namedType)
        {
            // Check for dictionary type - get key/value from IDictionary<TKey, TValue> interface
            if (IsDictionaryType(namedType))
            {
                var dictInterface = namedType.AllInterfaces.FirstOrDefault(i =>
                    i.OriginalDefinition.MetadataName == "IDictionary`2" &&
                    i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic");

                if (dictInterface != null && dictInterface.TypeArguments.Length == 2)
                {
                    keyTypeName = GetFullyQualifiedName(dictInterface.TypeArguments[0]);
                    valueTypeName = GetFullyQualifiedName(dictInterface.TypeArguments[1]);
                }
            }
            // Check for collection type - get element from IEnumerable<T> interface
            else if (isCollection)
            {
                var enumerableInterface = namedType.AllInterfaces.FirstOrDefault(i =>
                    i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

                if (enumerableInterface != null && enumerableInterface.TypeArguments.Length == 1)
                {
                    elementTypeName = GetFullyQualifiedName(enumerableInterface.TypeArguments[0]);
                }
            }
        }

        // Collect constructor parameters (for records and types with [JsonConstructor])
        var constructorParameters = CollectConstructorParameters(type, properties);
        var isRecord = IsRecordType(type);
        var isEnum = type.TypeKind == TypeKind.Enum;

        // Get type-level [JsonConverter] attribute
        string? converterTypeName = GetConverterType(type);

        // Check for polymorphism
        bool isPolymorphic = HasAttribute(type, "System.Text.Json.Serialization.JsonPolymorphicAttribute");
        string? typeDiscriminatorPropertyName = null;
        var derivedTypes = EquatableArray<DerivedTypeModel>.Empty;
        bool isAbstract = type.IsAbstract;

        if (isPolymorphic)
        {
            typeDiscriminatorPropertyName = GetTypeDiscriminatorPropertyName(type);
            derivedTypes = CollectDerivedTypes(type);
        }

        return new TypeModel(
            fqn,
            typeName,
            ns,
            isGeneric,
            isCollection,
            isValueType,
            hasParameterlessConstructor,
            properties,
            elementTypeName,
            keyTypeName,
            valueTypeName,
            constructorParameters,
            isRecord,
            isEnum,
            converterTypeName,
            isPolymorphic,
            typeDiscriminatorPropertyName,
            derivedTypes,
            isAbstract);
    }

    private static EquatableArray<PropertyModel> CollectProperties(ITypeSymbol type)
    {
        if (IsCollectionType(type) || type.TypeKind == TypeKind.Array)
        {
            return EquatableArray<PropertyModel>.Empty;
        }

        var properties = new List<PropertyModel>();
        var processedNames = new HashSet<string>();

        // Collect members from the type and all its base types
        var currentType = type;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            CollectPropertiesFromType(currentType, properties, processedNames);
            currentType = currentType.BaseType;
        }

        return properties.ToEquatableArray();
    }

    private static void CollectPropertiesFromType(ITypeSymbol type, List<PropertyModel> properties, HashSet<string> processedNames)
    {
        foreach (var member in type.GetMembers())
        {
            // Handle properties
            if (member is IPropertySymbol property && !property.IsStatic && !property.IsIndexer)
            {
                // Skip if already processed (e.g., overridden in derived class)
                if (processedNames.Contains(property.Name))
                    continue;

                bool hasJsonInclude = HasAttribute(property, "System.Text.Json.Serialization.JsonIncludeAttribute");
                bool isPublic = property.DeclaredAccessibility == Accessibility.Public;

                // Skip non-public properties without [JsonInclude]
                if (!isPublic && !hasJsonInclude)
                    continue;

                var hasGetter = property.GetMethod != null &&
                               (property.GetMethod.DeclaredAccessibility == Accessibility.Public || hasJsonInclude);
                var hasSetter = property.SetMethod != null &&
                               (property.SetMethod.DeclaredAccessibility == Accessibility.Public || hasJsonInclude);
                var isInitOnly = property.SetMethod?.IsInitOnly ?? false;

                // Check for [JsonIgnore]
                bool isIgnored = HasAttribute(property, "System.Text.Json.Serialization.JsonIgnoreAttribute");

                // Check for [JsonRequired]
                bool isRequired = HasAttribute(property, "System.Text.Json.Serialization.JsonRequiredAttribute");

                // Get [JsonPropertyName] value if present
                string jsonName = GetJsonPropertyName(property) ?? ToCamelCase(property.Name);

                // Get [JsonConverter] type if present
                string? converterType = GetConverterType(property);

                // Get [JsonNumberHandling] value if present
                string? numberHandling = GetNumberHandling(property);

                // Skip properties without getter (can't serialize)
                if (!hasGetter)
                    continue;

                processedNames.Add(property.Name);

                var propModel = new PropertyModel(
                    name: property.Name,
                    jsonName: jsonName,
                    typeFullyQualifiedName: GetFullyQualifiedName(property.Type),
                    hasGetter: hasGetter,
                    hasSetter: hasSetter,
                    isNullable: property.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                               (property.Type is INamedTypeSymbol nt && nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T),
                    isValueType: property.Type.IsValueType,
                    isIgnored: isIgnored,
                    isRequired: isRequired,
                    hasJsonInclude: hasJsonInclude,
                    isPublic: isPublic,
                    isInitOnly: isInitOnly,
                    isField: false,
                    converterTypeName: converterType,
                    numberHandling: numberHandling);

                properties.Add(propModel);
            }
            // Handle fields with [JsonInclude]
            else if (member is IFieldSymbol field && !field.IsStatic && !field.IsConst)
            {
                // Skip if already processed
                if (processedNames.Contains(field.Name))
                    continue;

                bool hasJsonInclude = HasAttribute(field, "System.Text.Json.Serialization.JsonIncludeAttribute");

                // Only include fields with [JsonInclude]
                if (!hasJsonInclude)
                    continue;

                bool isIgnored = HasAttribute(field, "System.Text.Json.Serialization.JsonIgnoreAttribute");
                bool isRequired = HasAttribute(field, "System.Text.Json.Serialization.JsonRequiredAttribute");
                bool fieldIsPublic = field.DeclaredAccessibility == Accessibility.Public;
                string jsonName = GetJsonPropertyNameFromField(field) ?? ToCamelCase(field.Name);
                string? converterType = GetConverterType(field);
                string? numberHandling = GetNumberHandling(field);

                processedNames.Add(field.Name);

                var propModel = new PropertyModel(
                    name: field.Name,
                    jsonName: jsonName,
                    typeFullyQualifiedName: GetFullyQualifiedName(field.Type),
                    hasGetter: true,
                    hasSetter: !field.IsReadOnly,
                    isNullable: field.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                               (field.Type is INamedTypeSymbol nt && nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T),
                    isValueType: field.Type.IsValueType,
                    isIgnored: isIgnored,
                    isRequired: isRequired,
                    hasJsonInclude: hasJsonInclude,
                    isPublic: fieldIsPublic,
                    isInitOnly: false,
                    isField: true,
                    converterTypeName: converterType,
                    numberHandling: numberHandling);

                properties.Add(propModel);
            }
        }
    }

    /// <summary>
    /// Gets the value of [JsonPropertyName] attribute from a field if present.
    /// </summary>
    private static string? GetJsonPropertyNameFromField(IFieldSymbol field)
    {
        foreach (var attr in field.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            if (attrClass.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute")
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string name)
                {
                    return name;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the converter type from [JsonConverter] attribute if present.
    /// </summary>
    private static string? GetConverterType(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            if (attrClass.ToDisplayString() == "System.Text.Json.Serialization.JsonConverterAttribute")
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol converterType)
                {
                    return GetFullyQualifiedName(converterType);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the JsonNumberHandling value if [JsonNumberHandling] attribute is present.
    /// </summary>
    private static string? GetNumberHandling(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            if (attrClass.ToDisplayString() == "System.Text.Json.Serialization.JsonNumberHandlingAttribute")
            {
                if (attr.ConstructorArguments.Length > 0)
                {
                    var value = attr.ConstructorArguments[0].Value;
                    if (value != null)
                    {
                        // Convert enum value to string representation
                        return $"(global::System.Text.Json.Serialization.JsonNumberHandling){value}";
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if a symbol has an attribute with the given fully qualified name.
    /// </summary>
    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            var fullName = attrClass.ToDisplayString();
            if (fullName == attributeFullName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the TypeDiscriminatorPropertyName from [JsonPolymorphic] attribute.
    /// </summary>
    private static string? GetTypeDiscriminatorPropertyName(ITypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            if (attrClass.ToDisplayString() == "System.Text.Json.Serialization.JsonPolymorphicAttribute")
            {
                // Check named arguments for TypeDiscriminatorPropertyName
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "TypeDiscriminatorPropertyName" && namedArg.Value.Value is string propName)
                    {
                        return propName;
                    }
                }
                // Default value is "$type"
                return "$type";
            }
        }
        return null;
    }

    /// <summary>
    /// Collects all [JsonDerivedType] attributes from a type.
    /// </summary>
    private static EquatableArray<DerivedTypeModel> CollectDerivedTypes(ITypeSymbol type)
    {
        var derivedTypes = new List<DerivedTypeModel>();

        foreach (var attr in type.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            if (attrClass.ToDisplayString() == "System.Text.Json.Serialization.JsonDerivedTypeAttribute")
            {
                // First constructor argument is the derived type
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol derivedType)
                {
                    string? discriminator = null;
                    bool isStringDiscriminator = true;

                    // Second constructor argument (if present) is the type discriminator
                    if (attr.ConstructorArguments.Length > 1)
                    {
                        var discriminatorArg = attr.ConstructorArguments[1];
                        if (discriminatorArg.Value is string strDiscriminator)
                        {
                            discriminator = strDiscriminator;
                            isStringDiscriminator = true;
                        }
                        else if (discriminatorArg.Value is int intDiscriminator)
                        {
                            discriminator = intDiscriminator.ToString();
                            isStringDiscriminator = false;
                        }
                    }

                    derivedTypes.Add(new DerivedTypeModel(
                        GetFullyQualifiedName(derivedType),
                        discriminator,
                        isStringDiscriminator));
                }
            }
        }

        return derivedTypes.ToEquatableArray();
    }

    /// <summary>
    /// Gets the value of [JsonPropertyName] attribute if present.
    /// </summary>
    private static string? GetJsonPropertyName(IPropertySymbol property)
    {
        foreach (var attr in property.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null) continue;

            if (attrClass.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute")
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string name)
                {
                    return name;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Collects constructor parameters for parameterized constructor deserialization.
    /// Finds [JsonConstructor] marked constructor, or primary constructor for records, or best matching constructor.
    /// </summary>
    public static EquatableArray<ConstructorParameterModel> CollectConstructorParameters(ITypeSymbol type, EquatableArray<PropertyModel> properties)
    {
        if (type is not INamedTypeSymbol namedType)
            return EquatableArray<ConstructorParameterModel>.Empty;

        // Skip collection types
        if (IsCollectionType(type) || type.TypeKind == TypeKind.Array)
            return EquatableArray<ConstructorParameterModel>.Empty;

        var constructors = namedType.Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        if (constructors.Count == 0)
            return EquatableArray<ConstructorParameterModel>.Empty;

        // Priority 1: [JsonConstructor] marked constructor
        var jsonConstructor = constructors.FirstOrDefault(c => HasAttribute(c, "System.Text.Json.Serialization.JsonConstructorAttribute"));

        // Priority 2: Primary constructor (for records - typically the one with most parameters matching properties)
        // Priority 3: Constructor with most parameters matching property names
        var selectedCtor = jsonConstructor ?? FindBestConstructor(constructors, properties);

        if (selectedCtor == null || selectedCtor.Parameters.Length == 0)
            return EquatableArray<ConstructorParameterModel>.Empty;

        var parameters = new List<ConstructorParameterModel>();
        foreach (var param in selectedCtor.Parameters)
        {
            // Find matching property by comparing parameter name (case-insensitive) with property names
            var matchingProp = FindMatchingProperty(param, properties);

            // Get default value if present
            string? defaultValueString = null;
            if (param.HasExplicitDefaultValue)
            {
                defaultValueString = FormatDefaultValue(param.ExplicitDefaultValue, param.Type);
            }

            var ctorParam = new ConstructorParameterModel(
                name: param.Name,
                jsonName: matchingProp?.JsonName ?? ToCamelCase(param.Name),
                typeFullyQualifiedName: GetFullyQualifiedName(param.Type),
                position: param.Ordinal,
                hasDefaultValue: param.HasExplicitDefaultValue,
                defaultValueString: defaultValueString);

            parameters.Add(ctorParam);
        }

        return parameters.ToEquatableArray();
    }

    private static IMethodSymbol? FindBestConstructor(List<IMethodSymbol> constructors, EquatableArray<PropertyModel> properties)
    {
        // Sort by parameter count (descending) to prefer constructors with more parameters
        var sorted = constructors.OrderByDescending(c => c.Parameters.Length).ToList();

        foreach (var ctor in sorted)
        {
            // Check if all parameters can be matched to properties
            bool allMatch = true;
            foreach (var param in ctor.Parameters)
            {
                var match = FindMatchingProperty(param, properties);
                if (match == null)
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch && ctor.Parameters.Length > 0)
                return ctor;
        }

        // If no constructor has all parameters matching, return the one with most matching parameters
        return sorted.FirstOrDefault(c => c.Parameters.Length > 0 && c.Parameters.Any(p => FindMatchingProperty(p, properties) != null));
    }

    private static PropertyModel? FindMatchingProperty(IParameterSymbol param, EquatableArray<PropertyModel> properties)
    {
        // Match by name (case-insensitive comparison)
        foreach (var prop in properties)
        {
            if (string.Equals(param.Name, prop.Name, StringComparison.OrdinalIgnoreCase))
            {
                return prop;
            }
        }
        return null;
    }

    private static string? FormatDefaultValue(object? value, ITypeSymbol type)
    {
        if (value == null)
            return "null";

        if (value is string s)
            return $"\"{s.Replace("\"", "\\\"")}\"";

        if (value is bool b)
            return b ? "true" : "false";

        if (value is char c)
            return $"'{c}'";

        if (type.TypeKind == TypeKind.Enum)
        {
            // Format as EnumType.Value
            return $"({GetFullyQualifiedName(type)}){value}";
        }

        // Numeric types
        return value.ToString();
    }

    /// <summary>
    /// Checks if the type is a record type.
    /// </summary>
    public static bool IsRecordType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            return namedType.IsRecord;
        }
        return false;
    }

    private static bool HasPublicParameterlessConstructor(ITypeSymbol type)
    {
        if (type.IsValueType)
            return true; // Structs always have parameterless constructor

        if (type is INamedTypeSymbol namedType)
        {
            // Check for public parameterless constructor
            foreach (var ctor in namedType.Constructors)
            {
                if (ctor.DeclaredAccessibility == Accessibility.Public &&
                    ctor.Parameters.Length == 0 &&
                    !ctor.IsStatic)
                {
                    return true;
                }
            }

            // If no constructors are declared explicitly, there's an implicit parameterless constructor
            if (!namedType.Constructors.Any(c => !c.IsStatic && !c.IsImplicitlyDeclared))
            {
                return true;
            }
        }

        return false;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Strip leading underscores (common for backing fields)
        int startIndex = 0;
        while (startIndex < name.Length && name[startIndex] == '_')
        {
            startIndex++;
        }

        if (startIndex >= name.Length)
            return name; // All underscores, return as-is

        // Get the name without leading underscores
        var strippedName = name.Substring(startIndex);

        if (string.IsNullOrEmpty(strippedName))
            return name;

        if (char.IsLower(strippedName[0]))
            return strippedName;

        return char.ToLowerInvariant(strippedName[0]) + strippedName.Substring(1);
    }

    /// <summary>
    /// Gets the fully qualified name of a type for use in generated code.
    /// </summary>
    public static string GetFullyQualifiedName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Gets the simple type name (without namespace).
    /// </summary>
    public static string GetTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    /// <summary>
    /// Checks if the type is a primitive type that doesn't need registration.
    /// System.Text.Json handles these natively.
    /// </summary>
    public static bool IsPrimitiveType(ITypeSymbol type)
    {
        // Check SpecialType first for built-in types
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_Char:
            case SpecialType.System_String:
            case SpecialType.System_Object:
            case SpecialType.System_DateTime:
                return true;
        }

        // Check by name for other well-known types
        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var withoutGlobal = fqn.StartsWith("global::") ? fqn.Substring(8) : fqn;

        if (PrimitiveTypeNames.Contains(withoutGlobal))
            return true;

        // Nullable value types - if the underlying type is primitive, so is the nullable
        if (type is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            var originalDef = namedType.OriginalDefinition.ToDisplayString();
            if (originalDef == "System.Nullable<T>" && namedType.TypeArguments.Length == 1)
            {
                return IsPrimitiveType(namedType.TypeArguments[0]);
            }
        }

        // Enum types are supported natively by STJ
        if (type.TypeKind == TypeKind.Enum)
            return true;

        return false;
    }

    /// <summary>
    /// Checks if the type is a collection type (including user-defined collections).
    /// Uses Roslyn's SpecialType for reliable detection.
    /// </summary>
    public static bool IsCollectionType(ITypeSymbol type)
    {
        // String implements IEnumerable<char> but is not a collection for JSON purposes
        if (type.SpecialType == SpecialType.System_String)
            return false;

        if (type is IArrayTypeSymbol)
            return true;

        if (type is INamedTypeSymbol namedType)
        {
            // Check if implements IEnumerable<T> using Roslyn's SpecialType
            // This works for all collections: List<T>, HashSet<T>, user-defined, etc.
            return namedType.AllInterfaces.Any(i =>
                i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        }

        return false;
    }

    /// <summary>
    /// Checks if the type is a dictionary type (including user-defined dictionaries).
    /// Checks for IDictionary&lt;TKey, TValue&gt; implementation.
    /// </summary>
    public static bool IsDictionaryType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            // Check if implements IDictionary<TKey, TValue>
            // IDictionary doesn't have a SpecialType, so we check by name
            return namedType.AllInterfaces.Any(i =>
                i.OriginalDefinition.MetadataName == "IDictionary`2" &&
                i.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic");
        }
        return false;
    }
}
