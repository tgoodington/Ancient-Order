using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Architecture;

public class BannedApiTests
{
    private const string RandomNumberGeneratorType = "System.Security.Cryptography.RandomNumberGenerator";

    private static readonly HashSet<string> BannedMembers = new(StringComparer.Ordinal)
    {
        "System.DateTime::get_Now",
        "System.DateTime::get_UtcNow",
        "System.DateTime::get_Today",
        "System.DateTimeOffset::get_Now",
        "System.DateTimeOffset::get_UtcNow",
        "System.Guid::NewGuid",
        "System.Collections.Generic.List`1::Sort",
        "System.Array::Sort",
        "System.MemoryExtensions::Sort",
        "System.Collections.Immutable.ImmutableArray`1::Sort",
    };

    private static readonly HashSet<string> BannedMathMembers = new(StringComparer.Ordinal)
    {
        "Round", "Pow", "Exp", "Log", "Log10", "Log2", "Sqrt", "Cbrt", "Sin", "Cos", "Tan", "Asin", "Acos", "Atan",
        "Atan2", "Sinh", "Cosh", "Tanh", "Asinh", "Acosh", "Atanh", "FusedMultiplyAdd", "ScaleB",
        "BitIncrement", "BitDecrement", "ReciprocalEstimate", "ReciprocalSqrtEstimate", "SinCos",
    };

    private static readonly string[] BannedTypeNames =
    [
        "System.Random",
        "System.MathF",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Immutable.ImmutableSortedDictionary",
        "System.Collections.Immutable.ImmutableSortedDictionary`2",
    ];

    private static readonly Dictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => (ushort)o.Value);

    [Fact]
    public void Rules_assembly_uses_no_banned_api()
    {
        var violations = Scan(typeof(Result<>).Assembly.Location);

        Assert.True(
            violations.Count == 0,
            "Banned API use in AncientOrder.Rules:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Scanner_detects_banned_use_in_a_probe_assembly()
    {
        var violations = Scan(typeof(BannedApiTests).Assembly.Location);

        Assert.Contains(violations, v => v.Contains("System.Math::Round called from", StringComparison.Ordinal) && v.EndsWith(".Use", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("System.Guid::NewGuid called from", StringComparison.Ordinal) && v.EndsWith(".Use", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("System.Collections.Generic.List`1::Sort called from", StringComparison.Ordinal) && v.EndsWith(".Use", StringComparison.Ordinal));
        Assert.Contains(violations, v => v == "System.Random (type reference)");
    }

    private static List<string> Scan(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var violations = new List<string>();

        foreach (var handle in md.TypeReferences)
        {
            var name = TypeRefName(md, handle);
            var banned = BannedTypeNames.Contains(name)
                || name.StartsWith("System.Runtime.Intrinsics", StringComparison.Ordinal)
                || name.StartsWith("System.Numerics.Vector", StringComparison.Ordinal);
            if (banned)
            {
                violations.Add($"{name} (type reference)");
            }
        }

        foreach (var typeHandle in md.TypeDefinitions)
        {
            var caller = OutermostTypeName(md, typeHandle);
            foreach (var methodHandle in md.GetTypeDefinition(typeHandle).GetMethods())
            {
                var method = md.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0)
                {
                    continue;
                }

                var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
                foreach (var token in MethodTokens(il))
                {
                    var target = ResolveCallTarget(md, token);
                    if (target is null)
                    {
                        continue;
                    }

                    var (type, member) = target.Value;
                    var full = $"{type}::{member}";
                    var banned = BannedMembers.Contains(full)
                        || (type == "System.Math" && BannedMathMembers.Contains(member))
                        || type == RandomNumberGeneratorType;
                    var allowed = (full == "System.Guid::NewGuid" && caller == "AncientOrder.Rules.Common.GuidIdSource")
                        || (type == RandomNumberGeneratorType && caller == "AncientOrder.Rules.Common.SystemSeedSource");
                    if (banned && !allowed)
                    {
                        violations.Add($"{full} called from {caller}.{md.GetString(method.Name)}");
                    }
                }
            }
        }

        return violations;
    }

    private static IEnumerable<int> MethodTokens(byte[] il)
    {
        var i = 0;
        while (i < il.Length)
        {
            ushort code = il[i++];
            if (code == 0xFE)
            {
                code = (ushort)(0xFE00 | il[i++]);
            }

            var op = OpCodesByValue[code];
            switch (op.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    i += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;
                case OperandType.InlineSwitch:
                    var count = BitConverter.ToInt32(il, i);
                    i += 4 + (4 * count);
                    break;
                case OperandType.InlineMethod:
                    yield return BitConverter.ToInt32(il, i);
                    i += 4;
                    break;
                default:
                    i += 4;
                    break;
            }
        }
    }

    private static (string Type, string Member)? ResolveCallTarget(MetadataReader md, int token)
    {
        var handle = MetadataTokens.EntityHandle(token);
        if (handle.Kind == HandleKind.MethodSpecification)
        {
            handle = md.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
        }

        if (handle.Kind != HandleKind.MemberReference)
        {
            return null;
        }

        var reference = md.GetMemberReference((MemberReferenceHandle)handle);
        var member = md.GetString(reference.Name);
        switch (reference.Parent.Kind)
        {
            case HandleKind.TypeReference:
                return (TypeRefName(md, (TypeReferenceHandle)reference.Parent), member);
            case HandleKind.TypeSpecification:
                var blob = md.GetBlobReader(md.GetTypeSpecification((TypeSpecificationHandle)reference.Parent).Signature);
                if (blob.ReadByte() != (byte)SignatureTypeCode.GenericTypeInstance)
                {
                    return null;
                }

                blob.ReadByte();
                var inner = blob.ReadTypeHandle();
                return inner.Kind == HandleKind.TypeReference ? (TypeRefName(md, (TypeReferenceHandle)inner), member) : null;
            default:
                return null;
        }
    }

    private static string TypeRefName(MetadataReader md, TypeReferenceHandle handle)
    {
        var reference = md.GetTypeReference(handle);
        var ns = md.GetString(reference.Namespace);
        var name = md.GetString(reference.Name);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    private static string OutermostTypeName(MetadataReader md, TypeDefinitionHandle handle)
    {
        var definition = md.GetTypeDefinition(handle);
        while (!definition.GetDeclaringType().IsNil)
        {
            definition = md.GetTypeDefinition(definition.GetDeclaringType());
        }

        var ns = md.GetString(definition.Namespace);
        var name = md.GetString(definition.Name);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    // Deliberately uses banned APIs so Scanner_detects_banned_use_in_a_probe_assembly can prove the scan works.
    // It is never called.
    internal static class BannedApiProbe
    {
        internal static object Use()
        {
            var list = new List<int> { 3, 1, 2 };
            list.Sort();
            return (Math.Round(1.5), Guid.NewGuid(), new Random(), list);
        }
    }
}
