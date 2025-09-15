using System.Runtime.CompilerServices;
using Microsoft.IdentityModel.Tokens;

namespace KestrunTests.Jwt;

internal static class TestCryptoInit
{
    [ModuleInitializer]
    public static void Init() =>
        // Enable ECDSA signing even on hosts where the default factory reports unsupported
        CryptoProviderFactory.Default = new EcdsaEnablingCryptoProviderFactory();
}
