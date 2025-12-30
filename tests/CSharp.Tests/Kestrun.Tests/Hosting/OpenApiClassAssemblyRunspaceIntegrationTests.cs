using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Reflection.Emit;
using Kestrun.Hosting;
using Kestrun.Runtime;
using Serilog;
using Xunit;

namespace KestrunTests.Hosting;

[Trait("Category", "Hosting")]
public class OpenApiClassAssemblyRunspaceIntegrationTests
{
    private static Assembly BuildDynamicAssemblyWithComponents()
    {
        var asmName = new AssemblyName("Dynamic.OpenApiComponents.Integration");
        var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
        var moduleBuilder = asmBuilder.DefineDynamicModule("Main");

        static CustomAttributeBuilder Cab<T>(params object?[] args)
        {
            var ctor = typeof(T).GetConstructors().First();
            return new CustomAttributeBuilder(ctor, args);
        }

        // Owner: [OpenApiSchemaComponent]
        var ownerType = moduleBuilder.DefineType("Owner", TypeAttributes.Public | TypeAttributes.Class);
        ownerType.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));

        var ownerNameField = ownerType.DefineField("_Name", typeof(string), FieldAttributes.Private);
        var ownerNameProp = ownerType.DefineProperty("Name", PropertyAttributes.None, typeof(string), Type.EmptyTypes);
        var getOwnerName = ownerType.DefineMethod("get_Name", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), Type.EmptyTypes);
        var ilGetOwnerName = getOwnerName.GetILGenerator();
        ilGetOwnerName.Emit(OpCodes.Ldarg_0);
        ilGetOwnerName.Emit(OpCodes.Ldfld, ownerNameField);
        ilGetOwnerName.Emit(OpCodes.Ret);
        var setOwnerName = ownerType.DefineMethod("set_Name", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [typeof(string)]);
        var ilSetOwnerName = setOwnerName.GetILGenerator();
        ilSetOwnerName.Emit(OpCodes.Ldarg_0);
        ilSetOwnerName.Emit(OpCodes.Ldarg_1);
        ilSetOwnerName.Emit(OpCodes.Stfld, ownerNameField);
        ilSetOwnerName.Emit(OpCodes.Ret);
        ownerNameProp.SetGetMethod(getOwnerName);
        ownerNameProp.SetSetMethod(setOwnerName);
        var ownerCreated = ownerType.CreateType()!;

        // Pet: [OpenApiSchemaComponent] with dependency on Owner
        var petType = moduleBuilder.DefineType("Pet", TypeAttributes.Public | TypeAttributes.Class);
        petType.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));

        var petOwnerField = petType.DefineField("_Owner", ownerCreated, FieldAttributes.Private);
        var petOwnerProp = petType.DefineProperty("Owner", PropertyAttributes.None, ownerCreated, Type.EmptyTypes);
        var getPetOwner = petType.DefineMethod("get_Owner", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, ownerCreated, Type.EmptyTypes);
        var ilGetPetOwner = getPetOwner.GetILGenerator();
        ilGetPetOwner.Emit(OpCodes.Ldarg_0);
        ilGetPetOwner.Emit(OpCodes.Ldfld, petOwnerField);
        ilGetPetOwner.Emit(OpCodes.Ret);
        var setPetOwner = petType.DefineMethod("set_Owner", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [ownerCreated]);
        var ilSetPetOwner = setPetOwner.GetILGenerator();
        ilSetPetOwner.Emit(OpCodes.Ldarg_0);
        ilSetPetOwner.Emit(OpCodes.Ldarg_1);
        ilSetPetOwner.Emit(OpCodes.Stfld, petOwnerField);
        ilSetPetOwner.Emit(OpCodes.Ret);
        petOwnerProp.SetGetMethod(getPetOwner);
        petOwnerProp.SetSetMethod(setPetOwner);

        _ = petType.CreateType();
        return asmBuilder;
    }

    [Fact]
    public void RunspacePool_Loads_CompiledOpenApiClassesAssembly_And_ResolvesTypes()
    {
        var asm = BuildDynamicAssemblyWithComponents();
        var dllPath = PowerShellOpenApiClassExporter.ExportOpenApiClasses([asm], Serilog.Log.Logger);
        Assert.False(string.IsNullOrWhiteSpace(dllPath));
        Assert.True(File.Exists(dllPath));
        Assert.EndsWith(".dll", dllPath, StringComparison.OrdinalIgnoreCase);

        using var host = new KestrunHost("Tests", Log.Logger);
        using var pool = host.CreateRunspacePool(1, openApiClassesPath: dllPath);

        var rs = pool.Acquire();
        try
        {
            using var ps = PowerShell.Create();
            ps.Runspace = rs;

            // If the assembly is correctly injected, these should resolve by simple name.
            _ = ps.AddScript("[Owner]::new() | Out-Null; [Pet]::new() | Out-Null; 'ok'");
            var output = ps.Invoke();

            Assert.Empty(ps.Streams.Error);
            Assert.Contains(output.Select(o => o?.ToString()), s => string.Equals(s, "ok", StringComparison.Ordinal));
        }
        finally
        {
            pool.Release(rs);
        }
    }
}
