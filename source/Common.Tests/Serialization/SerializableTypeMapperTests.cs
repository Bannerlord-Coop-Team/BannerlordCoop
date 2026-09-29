using Common.Serialization;
using ProtoBuf;
using System.Reflection;
using System.Reflection.Emit;

namespace Common.Tests.Serialization;

/// <summary>
/// Covers how <see cref="SerializableTypeMapper"/> maps types to wire ids and back.
/// </summary>
public class SerializableTypeMapperTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void TryGetType_NegativeId_ReturnsFalse(int id)
    {
        var mapper = new SerializableTypeMapper();

        Assert.False(mapper.TryGetType(id, out var type));
        Assert.Null(type);
    }

    [Fact]
    public void TryGetId_TypeParameter_ReturnsFalse()
    {
        var mapper = new SerializableTypeMapper();
        var typeParameter = typeof(List<>).GetGenericArguments()[0];
        var typeParameterArray = typeParameter.MakeArrayType();

        mapper.AddTypes(new[] { typeParameter, typeParameterArray });

        Assert.False(mapper.TryGetId(typeParameter, out var id));
        Assert.Equal(0, id);
        Assert.False(mapper.TryGetId(typeParameterArray, out var arrayId));
        Assert.Equal(0, arrayId);
    }

    [Fact]
    public void AddTypes_TypesAddedAgainOnRebind_KeepTheirId()
    {
        // On reconnect, AutoSyncPatcher.RebindHandlers adds types the mapper already has.
        var mapper = new SerializableTypeMapper();

        mapper.AddTypes(new[] { typeof(RebindProbe), typeof(RebindProbe) });
        Assert.True(mapper.TryGetId(typeof(RebindProbe), out var firstId));

        mapper.AddTypes(new[] { typeof(RebindProbe) });
        Assert.True(mapper.TryGetId(typeof(RebindProbe), out var secondId));

        Assert.Equal(firstId, secondId);
        Assert.True(mapper.TryGetType(firstId, out var type));
        Assert.Same(typeof(RebindProbe), type);
    }

    [Fact]
    public void AddTypes_TwoTypesWithTheSameId_Throws()
    {
        // These two names share a 32-bit FNV-1a hash, so renaming either or changing the hash breaks this test.
        var mapper = new SerializableTypeMapper();

        var exception = Assert.Throws<InvalidOperationException>(
            () => mapper.AddTypes(new[] { typeof(CollisionProbeAhnI), typeof(CollisionProbecEAA) }));

        Assert.Contains(typeof(CollisionProbeAhnI).FullName!, exception.Message);
        Assert.Contains(typeof(CollisionProbecEAA).FullName!, exception.Message);
        Assert.True(mapper.TryGetId(typeof(CollisionProbeAhnI), out var id));
        Assert.True(mapper.TryGetType(id, out var type));
        Assert.Same(typeof(CollisionProbeAhnI), type);
    }

    [Fact]
    public void AddTypes_OtherTypeWithTheSameFullName_KeepsTheFirstType()
    {
        var assemblyName = new AssemblyName($"{nameof(SerializableTypeMapperTests)}.{Guid.NewGuid()}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var duplicate = module.DefineType(typeof(DuplicateFullNameProbe).FullName!, TypeAttributes.NotPublic | TypeAttributes.Sealed).CreateType()!;
        var mapper = new SerializableTypeMapper();

        mapper.AddTypes(new[] { typeof(DuplicateFullNameProbe) });
        mapper.AddTypes(new[] { duplicate });

        Assert.True(mapper.TryGetId(typeof(DuplicateFullNameProbe), out var id));
        Assert.True(mapper.TryGetId(duplicate, out var duplicateId));
        Assert.Equal(id, duplicateId);
        Assert.True(mapper.TryGetType(id, out var type));
        Assert.Same(typeof(DuplicateFullNameProbe), type);
    }

    [Fact]
    public void TryGetId_CommonProtoContracts_AreNonNegative()
    {
        var mapper = new SerializableTypeMapper();
        var contracts = typeof(SerializableTypeMapper).Assembly.GetTypes()
            .Where(type => type.IsDefined(typeof(ProtoContractAttribute), inherit: false) && !type.ContainsGenericParameters)
            .ToArray();

        Assert.NotEmpty(contracts);
        foreach (var contract in contracts)
        {
            Assert.True(mapper.TryGetId(contract, out var id));
            Assert.True(id >= 0, $"{contract.FullName} maps to {id}");
        }
    }
}

/// <summary>
/// Type added more than once in <see cref="SerializableTypeMapperTests"/>.
/// </summary>
internal sealed class RebindProbe { }

/// <summary>
/// Shares its id with <see cref="CollisionProbecEAA"/>.
/// </summary>
internal sealed class CollisionProbeAhnI { }

/// <summary>
/// Shares its id with <see cref="CollisionProbeAhnI"/>.
/// </summary>
internal sealed class CollisionProbecEAA { }

/// <summary>
/// Static type whose full name a dynamic type copies in <see cref="SerializableTypeMapperTests"/>.
/// </summary>
internal sealed class DuplicateFullNameProbe { }
