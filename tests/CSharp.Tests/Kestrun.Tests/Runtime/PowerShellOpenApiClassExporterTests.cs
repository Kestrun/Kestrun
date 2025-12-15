using System.Reflection;
using System.Reflection.Emit;
using Kestrun.Runtime;
using Xunit;

namespace KestrunTests.Runtime;

public class PowerShellOpenApiClassExporterTests
{
    private static Assembly BuildDynamicAssemblyWithComponents()
    {
        var asmName = new AssemblyName("Dynamic.OpenApiComponents");
        var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
        var moduleBuilder = asmBuilder.DefineDynamicModule("Main");

        // Helper to create custom attribute builders
        static CustomAttributeBuilder Cab<T>(params object?[] args)
        {
            var ctor = typeof(T).GetConstructors().First();
            return new CustomAttributeBuilder(ctor, args);
        }

        // Base component class: [OpenApiSchemaComponent]
        var baseType = moduleBuilder.DefineType("PetBase", TypeAttributes.Public | TypeAttributes.Class);
        baseType.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));
        // Base property
        var idField = baseType.DefineField("_Id", typeof(int), FieldAttributes.Private);
        var idProp = baseType.DefineProperty("Id", PropertyAttributes.None, typeof(int), Type.EmptyTypes);
        var getId = baseType.DefineMethod("get_Id", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
        var ilGetId = getId.GetILGenerator();
        ilGetId.Emit(OpCodes.Ldarg_0);
        ilGetId.Emit(OpCodes.Ldfld, idField);
        ilGetId.Emit(OpCodes.Ret);
        var setId = baseType.DefineMethod("set_Id", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [typeof(int)]);
        var ilSetId = setId.GetILGenerator();
        ilSetId.Emit(OpCodes.Ldarg_0);
        ilSetId.Emit(OpCodes.Ldarg_1);
        ilSetId.Emit(OpCodes.Stfld, idField);
        ilSetId.Emit(OpCodes.Ret);
        idProp.SetGetMethod(getId);
        idProp.SetSetMethod(setId);
        var baseTypeCreated = baseType.CreateType()!;

        // Dependent component class: [OpenApiSchemaComponent]
        var ownerType = moduleBuilder.DefineType("Owner", TypeAttributes.Public | TypeAttributes.Class);
        ownerType.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));
        // Owner.Name:string
        var nameField = ownerType.DefineField("_Name", typeof(string), FieldAttributes.Private);
        var nameProp = ownerType.DefineProperty("Name", PropertyAttributes.None, typeof(string), Type.EmptyTypes);
        var getName = ownerType.DefineMethod("get_Name", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), Type.EmptyTypes);
        var ilGetName = getName.GetILGenerator();
        ilGetName.Emit(OpCodes.Ldarg_0);
        ilGetName.Emit(OpCodes.Ldfld, nameField);
        ilGetName.Emit(OpCodes.Ret);
        var setName = ownerType.DefineMethod("set_Name", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [typeof(string)]);
        var ilSetName = setName.GetILGenerator();
        ilSetName.Emit(OpCodes.Ldarg_0);
        ilSetName.Emit(OpCodes.Ldarg_1);
        ilSetName.Emit(OpCodes.Stfld, nameField);
        ilSetName.Emit(OpCodes.Ret);
        nameProp.SetGetMethod(getName);
        nameProp.SetSetMethod(setName);
        var ownerTypeCreated = ownerType.CreateType()!;

        // Main component with inheritance and property dependencies
        var petType = moduleBuilder.DefineType("Pet", TypeAttributes.Public | TypeAttributes.Class, baseTypeCreated);
        petType.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));
        // Pet.Owner: Owner
        var ownerField = petType.DefineField("_Owner", ownerTypeCreated, FieldAttributes.Private);
        var ownerProp2 = petType.DefineProperty("Owner", PropertyAttributes.None, ownerTypeCreated, Type.EmptyTypes);
        var getOwner = petType.DefineMethod("get_Owner", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, ownerTypeCreated, Type.EmptyTypes);
        var ilGetOwner = getOwner.GetILGenerator();
        ilGetOwner.Emit(OpCodes.Ldarg_0);
        ilGetOwner.Emit(OpCodes.Ldfld, ownerField);
        ilGetOwner.Emit(OpCodes.Ret);
        var setOwner = petType.DefineMethod("set_Owner", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [ownerTypeCreated]);
        var ilSetOwner = setOwner.GetILGenerator();
        ilSetOwner.Emit(OpCodes.Ldarg_0);
        ilSetOwner.Emit(OpCodes.Ldarg_1);
        ilSetOwner.Emit(OpCodes.Stfld, ownerField);
        ilSetOwner.Emit(OpCodes.Ret);
        ownerProp2.SetGetMethod(getOwner);
        ownerProp2.SetSetMethod(setOwner);

        // Pet.Tags: string[]
        var tagsField = petType.DefineField("_Tags", typeof(string[]), FieldAttributes.Private);
        var tagsProp = petType.DefineProperty("Tags", PropertyAttributes.None, typeof(string[]), Type.EmptyTypes);
        var getTags = petType.DefineMethod("get_Tags", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string[]), Type.EmptyTypes);
        var ilGetTags = getTags.GetILGenerator();
        ilGetTags.Emit(OpCodes.Ldarg_0);
        ilGetTags.Emit(OpCodes.Ldfld, tagsField);
        ilGetTags.Emit(OpCodes.Ret);
        var setTags = petType.DefineMethod("set_Tags", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [typeof(string[])]);
        var ilSetTags = setTags.GetILGenerator();
        ilSetTags.Emit(OpCodes.Ldarg_0);
        ilSetTags.Emit(OpCodes.Ldarg_1);
        ilSetTags.Emit(OpCodes.Stfld, tagsField);
        ilSetTags.Emit(OpCodes.Ret);
        tagsProp.SetGetMethod(getTags);
        tagsProp.SetSetMethod(setTags);

        // Finalize the Pet type so it can be loaded by reflection
        _ = petType.CreateType();

        return asmBuilder;
    }

    [Fact]
    public void ExportOpenApiClasses_WritesScript_WithExpectedClassShapes()
    {
        var asm = BuildDynamicAssemblyWithComponents();
        var path = PowerShellOpenApiClassExporter.ExportOpenApiClasses([asm]);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);

        // Header markers
        Assert.Contains("Kestrun OpenAPI Autogenerated Class Definitions", content);

        // Owner class
        Assert.Contains("class Owner {", content);
        Assert.Contains("[string]$Name", content);

        // PetBase inheritance and int property mapping
        Assert.Contains("class PetBase {", content);
        Assert.Contains("[int]$Id", content);

        // Pet : PetBase inheritance
        Assert.Contains("class Pet : PetBase {", content);

        // Pet.Owner property referencing component by simple name
        Assert.Contains("[Owner]$Owner", content);

        // Array mapping
        Assert.Contains("[string[]]$Tags", content);
    }
}
