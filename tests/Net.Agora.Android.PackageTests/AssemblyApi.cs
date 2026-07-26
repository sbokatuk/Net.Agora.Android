using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Net.Agora.Android.PackageTests;

/// <summary>
/// Reads the public API out of a binding assembly using metadata only. The assembly targets
/// *-android and references Mono.Android, so it cannot be loaded into the test process; the
/// metadata reader lets these tests run on a plain desktop runner with no emulator.
/// </summary>
public sealed class AssemblyApi : IDisposable
{
    private readonly PEReader _peReader;
    private readonly MetadataReader _metadata;
    private IReadOnlyList<string>? _publicTypes;

    public AssemblyApi(Stream assembly)
    {
        _peReader = new PEReader(assembly);
        _metadata = _peReader.GetMetadataReader();
    }

    /// <summary>Namespace-qualified names of every public top-level type.</summary>
    public IReadOnlyList<string> PublicTypes => _publicTypes ??= _metadata.TypeDefinitions
        .Select(_metadata.GetTypeDefinition)
        .Where(type => (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public)
        .Select(FullNameOf)
        .ToList();

    public IReadOnlyList<string> MethodsOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetMethods()
            .Select(_metadata.GetMethodDefinition)
            .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
            .Select(method => _metadata.GetString(method.Name))
            .ToList();
    }

    public IReadOnlyList<string> PropertiesOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetProperties()
            .Select(_metadata.GetPropertyDefinition)
            .Select(property => _metadata.GetString(property.Name))
            .ToList();
    }

    public IReadOnlyList<string> EventsOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetEvents()
            .Select(_metadata.GetEventDefinition)
            .Select(e => _metadata.GetString(e.Name))
            .ToList();
    }

    /// <summary>
    /// Every public method of a type with its return type's full name — enough to assert that
    /// an adapter really returns <c>System.Threading.Tasks.Task</c> rather than merely existing
    /// under the right name.
    /// </summary>
    public IReadOnlyList<(string Name, string ReturnType)> MethodSignaturesOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetMethods()
            .Select(_metadata.GetMethodDefinition)
            .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
            .Select(method => (
                _metadata.GetString(method.Name),
                method.DecodeSignature(NameOnlySignatureProvider.Instance, genericContext: null).ReturnType))
            .ToList();
    }

    /// <summary>
    /// Renders signature types as plain full names. Only as complete as these tests need:
    /// definitions and references (which is where Task, string and the binding's own types all
    /// land) come back namespace-qualified, everything exotic comes back as a placeholder that
    /// simply won't equal any expected name.
    /// </summary>
    private sealed class NameOnlySignatureProvider : ISignatureTypeProvider<string, object?>
    {
        public static readonly NameOnlySignatureProvider Instance = new();

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => $"System.{typeCode}";

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeDefinition(handle);
            var ns = reader.GetString(type.Namespace);
            var name = reader.GetString(type.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeReference(handle);
            var ns = reader.GetString(type.Namespace);
            var name = reader.GetString(type.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public string GetSZArrayType(string elementType) => $"{elementType}[]";
        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[…]";
        public string GetByReferenceType(string elementType) => $"{elementType}&";
        public string GetPointerType(string elementType) => $"{elementType}*";
        public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(", ", typeArguments)}>";
        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetPinnedType(string elementType) => elementType;
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }

    private TypeDefinition FindType(string typeFullName)
    {
        foreach (var handle in _metadata.TypeDefinitions)
        {
            var type = _metadata.GetTypeDefinition(handle);
            if (FullNameOf(type) == typeFullName)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"Type '{typeFullName}' is not defined in this assembly.");
    }

    private string FullNameOf(TypeDefinition type)
    {
        var name = _metadata.GetString(type.Name);
        var ns = _metadata.GetString(type.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public void Dispose() => _peReader.Dispose();
}
