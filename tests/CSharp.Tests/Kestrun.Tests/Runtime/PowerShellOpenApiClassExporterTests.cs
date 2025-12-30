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
        var baseTypeCreated = baseType.CreateType();

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
        var ownerTypeCreated = ownerType.CreateType();

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

    private static Assembly BuildDynamicAssemblyWithAnnotationTypeProperties()
    {
        var asmName = new AssemblyName("Dynamic.OpenApiComponents.WithAnnotations");
        var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
        var moduleBuilder = asmBuilder.DefineDynamicModule("Main");

        static CustomAttributeBuilder Cab<T>(params object?[] args)
        {
            var ctor = typeof(T).GetConstructors().First();
            return new CustomAttributeBuilder(ctor, args);
        }

        // Component class: [OpenApiSchemaComponent]
        var typeBuilder = moduleBuilder.DefineType("UsesAnnotationTypes", TypeAttributes.Public | TypeAttributes.Class);
        typeBuilder.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));

        // UsesAnnotationTypes.Kind : OaString
        var kindField = typeBuilder.DefineField("_Kind", typeof(OaString), FieldAttributes.Private);
        var kindProp = typeBuilder.DefineProperty("Kind", PropertyAttributes.None, typeof(OaString), Type.EmptyTypes);
        var getKind = typeBuilder.DefineMethod("get_Kind", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(OaString), Type.EmptyTypes);
        var ilGetKind = getKind.GetILGenerator();
        ilGetKind.Emit(OpCodes.Ldarg_0);
        ilGetKind.Emit(OpCodes.Ldfld, kindField);
        ilGetKind.Emit(OpCodes.Ret);
        var setKind = typeBuilder.DefineMethod("set_Kind", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [typeof(OaString)]);
        var ilSetKind = setKind.GetILGenerator();
        ilSetKind.Emit(OpCodes.Ldarg_0);
        ilSetKind.Emit(OpCodes.Ldarg_1);
        ilSetKind.Emit(OpCodes.Stfld, kindField);
        ilSetKind.Emit(OpCodes.Ret);
        kindProp.SetGetMethod(getKind);
        kindProp.SetSetMethod(setKind);

        // UsesAnnotationTypes.Amount : OaNumber
        var amountField = typeBuilder.DefineField("_Amount", typeof(OaNumber), FieldAttributes.Private);
        var amountProp = typeBuilder.DefineProperty("Amount", PropertyAttributes.None, typeof(OaNumber), Type.EmptyTypes);
        var getAmount = typeBuilder.DefineMethod("get_Amount", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(OaNumber), Type.EmptyTypes);
        var ilGetAmount = getAmount.GetILGenerator();
        ilGetAmount.Emit(OpCodes.Ldarg_0);
        ilGetAmount.Emit(OpCodes.Ldfld, amountField);
        ilGetAmount.Emit(OpCodes.Ret);
        var setAmount = typeBuilder.DefineMethod("set_Amount", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [typeof(OaNumber)]);
        var ilSetAmount = setAmount.GetILGenerator();
        ilSetAmount.Emit(OpCodes.Ldarg_0);
        ilSetAmount.Emit(OpCodes.Ldarg_1);
        ilSetAmount.Emit(OpCodes.Stfld, amountField);
        ilSetAmount.Emit(OpCodes.Ret);
        amountProp.SetGetMethod(getAmount);
        amountProp.SetSetMethod(setAmount);

        _ = typeBuilder.CreateType();

        return asmBuilder;
    }

    [Fact]
    public void ExportOpenApiClasses_CompilesDll_WithExpectedTypeShapes()
    {
        var asm = BuildDynamicAssemblyWithComponents();
        var path = PowerShellOpenApiClassExporter.ExportOpenApiClasses([asm], Serilog.Log.Logger);

        Assert.True(File.Exists(path));

        Assert.EndsWith(".dll", path, StringComparison.OrdinalIgnoreCase);

        // Load the compiled assembly and verify types/properties exist
        var compiled = Assembly.LoadFrom(path);

        var owner = compiled.GetType("Owner", throwOnError: true)!;
        var petBase = compiled.GetType("PetBase", throwOnError: true)!;
        var pet = compiled.GetType("Pet", throwOnError: true)!;

        // Owner.Name : string
        var nameProp = owner.GetProperty("Name")!;
        Assert.Equal(typeof(string), nameProp.PropertyType);

        // PetBase.Id : int
        var idProp = petBase.GetProperty("Id")!;
        Assert.Equal(typeof(int), idProp.PropertyType);

        // Pet : PetBase
        Assert.Equal(petBase, pet.BaseType);

        // Pet.Owner : Owner
        var ownerProp = pet.GetProperty("Owner")!;
        Assert.Equal(owner, ownerProp.PropertyType);

        // Pet.Tags : string[]
        var tagsProp = pet.GetProperty("Tags")!;
        Assert.Equal(typeof(string[]), tagsProp.PropertyType);

        // Cache behavior: second call should return same path for same source
        var path2 = PowerShellOpenApiClassExporter.ExportOpenApiClasses([asm], Serilog.Log.Logger);
        Assert.Equal(path, path2);
    }

    [Fact]
    public void ExportOpenApiClasses_CompilesDll_WhenPropertyTypesComeFromKestrunAnnotations()
    {
        var asm = BuildDynamicAssemblyWithAnnotationTypeProperties();

        var path = PowerShellOpenApiClassExporter.ExportOpenApiClasses([asm], Serilog.Log.Logger);
        Assert.True(File.Exists(path));

        var compiled = Assembly.LoadFrom(path);
        var t = compiled.GetType("UsesAnnotationTypes", throwOnError: true)!;

        // These are emitted as simple names (global namespace) and should compile once Kestrun.Annotations.dll is referenced.
        Assert.Equal("OaString", t.GetProperty("Kind")!.PropertyType.Name);
        Assert.Equal("OaNumber", t.GetProperty("Amount")!.PropertyType.Name);
    }
}
