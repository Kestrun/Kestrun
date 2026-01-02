using System.Reflection;
using System.Reflection.Emit;
using Kestrun.Runtime;
using Xunit;

namespace KestrunTests.Runtime;

public class PowerShellOpenApiClassExporterTests
{
    // Static component types for ToPowerShellTypeName coverage (simpler than Reflection.Emit)
    [OpenApiSchemaComponent]
    private sealed class TypeNameCoverageComponent
    {
        public bool Bool { get; set; }
        public byte Byte { get; set; }
        public sbyte SByte { get; set; }
        public short Short { get; set; }
        public ushort UShort { get; set; }
        public int Int { get; set; }
        public uint UInt { get; set; }
        public long Long { get; set; }
        public ulong ULong { get; set; }
        public float Float { get; set; }
        public double Double { get; set; }
        public decimal Decimal { get; set; }
        public char Char { get; set; }
        public string String { get; set; } = string.Empty;
        public object Object { get; set; } = new();
        public DateTime DateTime { get; set; }
        public Guid Guid { get; set; }
        public byte[] Bytes { get; set; } = [];

        // Nullable branch
        public int? NullableInt { get; set; }
        public Guid? NullableGuid { get; set; }

        // Arrays branch
        public int[] Ints { get; set; } = [];

        // Fallback branch (not a primitive alias and not a component)
        public System.Net.IPAddress? Address { get; set; }

        // OpenApiValue<T> collapsing branch
        public OpenApiString? WrappedString { get; set; }
        public OpenApiInteger? WrappedInteger { get; set; }
        public OpenApiNumber? WrappedNumber { get; set; }
        public OpenApiBoolean? WrappedBoolean { get; set; }
    }

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

        // ---------------------------------------------------------
        // Array-wrapper schema component pattern:
        //   [OpenApiSchemaComponent(Array=true)] class EventDates : Date {}
        // Any property typed as EventDates should be emitted as [Date[]].

        // Date component
        var dateType = moduleBuilder.DefineType("Date", TypeAttributes.Public | TypeAttributes.Class, typeof(OpenApiString));
        dateType.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));
        var dateTypeCreated = dateType.CreateType()!;

        // EventDates component: Array = true, inherits from Date
        var eventDatesType = moduleBuilder.DefineType("EventDates", TypeAttributes.Public | TypeAttributes.Class, dateTypeCreated);
        {
            var ctor = typeof(OpenApiSchemaComponent).GetConstructors().First();
            var arrayProp = typeof(OpenApiSchemaComponent).GetProperty("Array")!;
            var cabArray = new CustomAttributeBuilder(ctor, [], [arrayProp], [true]);
            eventDatesType.SetCustomAttribute(cabArray);
        }
        var eventDatesTypeCreated = eventDatesType.CreateType()!;

        // UpdateSpecialEventRequest component referencing EventDates
        var updateType = moduleBuilder.DefineType("UpdateSpecialEventRequest", TypeAttributes.Public | TypeAttributes.Class);
        updateType.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));
        var datesField = updateType.DefineField("_Dates", eventDatesTypeCreated, FieldAttributes.Private);
        var datesProp = updateType.DefineProperty("Dates", PropertyAttributes.None, eventDatesTypeCreated, Type.EmptyTypes);
        var getDates = updateType.DefineMethod("get_Dates", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, eventDatesTypeCreated, Type.EmptyTypes);
        var ilGetDates = getDates.GetILGenerator();
        ilGetDates.Emit(OpCodes.Ldarg_0);
        ilGetDates.Emit(OpCodes.Ldfld, datesField);
        ilGetDates.Emit(OpCodes.Ret);
        var setDates = updateType.DefineMethod("set_Dates", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [eventDatesTypeCreated]);
        var ilSetDates = setDates.GetILGenerator();
        ilSetDates.Emit(OpCodes.Ldarg_0);
        ilSetDates.Emit(OpCodes.Ldarg_1);
        ilSetDates.Emit(OpCodes.Stfld, datesField);
        ilSetDates.Emit(OpCodes.Ret);
        datesProp.SetGetMethod(getDates);
        datesProp.SetSetMethod(setDates);

        // EventPrice : OpenApiNumber (should map to [double])
        var eventPriceType = moduleBuilder.DefineType("EventPrice", TypeAttributes.Public | TypeAttributes.Class, typeof(OpenApiNumber));
        eventPriceType.SetCustomAttribute(Cab<OpenApiSchemaComponent>([]));
        var eventPriceTypeCreated = eventPriceType.CreateType()!;

        var priceField = updateType.DefineField("_Price", eventPriceTypeCreated, FieldAttributes.Private);
        var priceProp = updateType.DefineProperty("Price", PropertyAttributes.None, eventPriceTypeCreated, Type.EmptyTypes);
        var getPrice = updateType.DefineMethod("get_Price", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, eventPriceTypeCreated, Type.EmptyTypes);
        var ilGetPrice = getPrice.GetILGenerator();
        ilGetPrice.Emit(OpCodes.Ldarg_0);
        ilGetPrice.Emit(OpCodes.Ldfld, priceField);
        ilGetPrice.Emit(OpCodes.Ret);
        var setPrice = updateType.DefineMethod("set_Price", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [eventPriceTypeCreated]);
        var ilSetPrice = setPrice.GetILGenerator();
        ilSetPrice.Emit(OpCodes.Ldarg_0);
        ilSetPrice.Emit(OpCodes.Ldarg_1);
        ilSetPrice.Emit(OpCodes.Stfld, priceField);
        ilSetPrice.Emit(OpCodes.Ret);
        priceProp.SetGetMethod(getPrice);
        priceProp.SetSetMethod(setPrice);

        _ = updateType.CreateType();

        return asmBuilder;
    }

    [Fact]
    public void ExportOpenApiClasses_WritesScript_WithExpectedClassShapes()
    {
        var asm = BuildDynamicAssemblyWithComponents();
        var path = PowerShellOpenApiClassExporter.ExportOpenApiClasses(assemblies: [asm], userCallbacks: null);

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

        // Array-wrapper mapping: EventDates should render as Date[] when referenced
        Assert.Contains("class EventDates : Date {", content);
        Assert.Contains("class UpdateSpecialEventRequest {", content);
        Assert.Contains("[string[]]$Dates", content);
        Assert.DoesNotContain("[EventDates]$Dates", content);
        Assert.DoesNotContain("[Date[]]$Dates", content);

        // Primitive collapsing: EventPrice : OpenApiNumber => [double]
        Assert.Contains("[double]$Price", content);
        Assert.DoesNotContain("[EventPrice]$Price", content);
    }

    [Fact]
    public void ExportOpenApiClasses_ExportsCallbackFunctionStubs_StripsAttributes_AndDetectsBodyParameter()
    {
        var callbacks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Intentionally unsorted casing; exporter sorts by key with OrdinalIgnoreCase
            ["zetaCallback"] = @"
function zetaCallback {
    [OpenApiCallback(Expression = '$request.body#/callbackUrls/status', HttpVerb = 'post', Pattern = '/v1/payments/{paymentId}/status', Inline = $true)]
    param(
        [OpenApiParameter(In = 'path', Required = $true)]
        [string]$paymentId,

        [OpenApiRequestBody(ContentType = 'application/json')]
        [PaymentStatusChangedEvent]$Payload
    )
    Write-Host 'ignored'
}
",
            ["AlphaCallback"] = @"
function AlphaCallback {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Count
    )
}
"
        };

        var path = PowerShellOpenApiClassExporter.ExportOpenApiClasses(assemblies: [], userCallbacks: callbacks);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);

        Assert.Contains("Kestrun User Callback Functions", content);

        // Ensure callback block is sorted case-insensitively: AlphaCallback before zetaCallback
        var alphaIdx = content.IndexOf("function AlphaCallback", StringComparison.Ordinal);
        var zetaIdx = content.IndexOf("function zetaCallback", StringComparison.Ordinal);
        Assert.True(alphaIdx >= 0, "AlphaCallback not found");
        Assert.True(zetaIdx >= 0, "zetaCallback not found");
        Assert.True(alphaIdx < zetaIdx, "Callbacks not sorted by name");

        // Attributes should be stripped from the param block
        Assert.DoesNotContain("OpenApiCallback", content);
        Assert.DoesNotContain("OpenApiParameter", content);
        Assert.DoesNotContain("OpenApiRequestBody", content);
        Assert.DoesNotContain("Parameter(", content);

        // But type constraints should remain
        Assert.Contains("[string]$paymentId", content);
        Assert.Contains("[PaymentStatusChangedEvent]$Payload", content);
        Assert.Contains("[int]$Count", content);

        // Body parameter should be the one annotated with OpenApiRequestBody
        Assert.Contains("$bodyParameterName = 'Payload'", content);

        // Wrapper should always call AddCallbackParameters
        Assert.Contains("$Context.Response.AddCallbackParameters(", content);
    }

    [Fact]
    public void ExportOpenApiClasses_ExportsCallbackFunctionStub_WithEmptyParamBlock_WhenNoParamFound()
    {
        var callbacks = new Dictionary<string, string>
        {
            ["noParamCallback"] = "function noParamCallback { Write-Host 'hi' }"
        };

        var path = PowerShellOpenApiClassExporter.ExportOpenApiClasses(assemblies: [], userCallbacks: callbacks);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);

        Assert.Contains("function noParamCallback", content);
        Assert.Contains("param()", content);
        Assert.Contains("$bodyParameterName = $null", content);
        Assert.Contains("$Context.Response.AddCallbackParameters(", content);
    }

    [Fact]
    public void ExportOpenApiClasses_MapsPrimitiveAliases_Nullables_Arrays_Fallback_AndOpenApiWrappers()
    {
        var asm = typeof(TypeNameCoverageComponent).Assembly;
        var path = PowerShellOpenApiClassExporter.ExportOpenApiClasses(assemblies: [asm], userCallbacks: null);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);

        Assert.Contains("class TypeNameCoverageComponent", content);

        // Primitive aliases
        Assert.Contains("[bool]$Bool", content);
        Assert.Contains("[byte]$Byte", content);
        Assert.Contains("[sbyte]$SByte", content);
        Assert.Contains("[short]$Short", content);
        Assert.Contains("[ushort]$UShort", content);
        Assert.Contains("[int]$Int", content);
        Assert.Contains("[uint]$UInt", content);
        Assert.Contains("[long]$Long", content);
        Assert.Contains("[ulong]$ULong", content);
        Assert.Contains("[float]$Float", content);
        Assert.Contains("[double]$Double", content);
        Assert.Contains("[decimal]$Decimal", content);
        Assert.Contains("[char]$Char", content);
        Assert.Contains("[string]$String", content);
        Assert.Contains("[object]$Object", content);
        Assert.Contains("[datetime]$DateTime", content);
        Assert.Contains("[guid]$Guid", content);
        Assert.Contains("[byte[]]$Bytes", content);

        // Nullable
        Assert.Contains("[Nullable[int]]$NullableInt", content);
        Assert.Contains("[Nullable[guid]]$NullableGuid", content);

        // Arrays
        Assert.Contains("[int[]]$Ints", content);

        // Fallback
        Assert.Contains("[System.Net.IPAddress]$Address", content);

        // OpenApiValue<T> collapsing
        Assert.Contains("[string]$WrappedString", content);
        Assert.Contains("[long]$WrappedInteger", content);
        Assert.Contains("[double]$WrappedNumber", content);
        Assert.Contains("[bool]$WrappedBoolean", content);
    }
}
