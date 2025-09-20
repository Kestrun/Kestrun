using System.Runtime.CompilerServices;
using Microsoft.IdentityModel.Tokens;

namespace KestrunTests.Jwt;

internal static class TestCryptoInit
{
    [ModuleInitializer]
    // Enable ECDSA signing even on hosts where the default factory reports unsupported
    public static void Init() =>
        CryptoProviderFactory.Default = new EcdsaEnablingCryptoProviderFactory();

}
