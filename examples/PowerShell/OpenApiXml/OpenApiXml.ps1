
param()
<#
.SYNOPSIS
    Kestrun PowerShell Example: OpenAPI XML Modeling
.DESCRIPTION
    This script demonstrates how to use OpenAPI XML modeling with attributes,
    namespaces, and wrapped arrays in Kestrun. Shows the new OpenApiXml attribute
    for configuring XML-specific serialization in OpenAPI 3.2.
#>

try {
    # Get the path of the current script
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    $powerShellExamplesPath = (Split-Path -Parent ($ScriptPath))
    $examplesPath = (Split-Path -Parent ($powerShellExamplesPath))
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"
    
    # Import the Kestrun module
    if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    } else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
} catch {
    Write-Error "Failed to import Kestrun module: $_"
    Write-Error 'Ensure the Kestrun module is installed or the path is correct.'
    exit 1
}

# Configure logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

# Create server
$server = New-KrServer -Name 'OpenAPI XML Example'

# Configure OpenAPI
$server | Enable-KrConfiguration -Name OpenAPI
$server.Options.OpenApiOptions.Title = 'Product API with XML Modeling'
$server.Options.OpenApiOptions.Version = '1.0'
$server.Options.OpenApiOptions.Description = 'Example demonstrating OpenAPI 3.2 XML modeling with attributes, namespaces, and wrapped arrays using the OpenApiXml attribute'

# Define C# Product class with XML attributes inline
# This demonstrates how to use both OpenApiProperty and OpenApiXml attributes
$productClassDefinition = @'
using System;

/// <summary>
/// Product model demonstrating OpenAPI XML attributes.
/// </summary>
[OpenApiSchemaComponent(Title = "Product", Description = "A product with XML-specific serialization")]
public class Product
{
    /// <summary>
    /// Product ID - rendered as XML attribute
    /// </summary>
    [OpenApiProperty(Description = "Unique product identifier")]
    [OpenApiXml(Name = "id", Attribute = true)]
    public int Id { get; set; }

    /// <summary>
    /// Product name with custom element name
    /// </summary>
    [OpenApiProperty(Description = "Product name")]
    [OpenApiXml(Name = "ProductName")]
    public string Name { get; set; }

    /// <summary>
    /// Product price with custom namespace and prefix
    /// </summary>
    [OpenApiProperty(Description = "Product price in USD")]
    [OpenApiXml(Name = "Price", Namespace = "http://example.com/pricing", Prefix = "price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Array of items with wrapped XML
    /// </summary>
    [OpenApiProperty(Description = "List of product items")]
    [OpenApiXml(Name = "Item", Wrapped = true)]
    public string[] Items { get; set; }
}
'@

# Compile the class definition
Add-Type -TypeDefinition $productClassDefinition

# Add a route that returns XML using the Product class
$server | Add-KrMapRoute -Verbs Get -Pattern '/products/{id}' -ScriptBlock {
    $id = Get-KrRequestRouteParam -Name 'id'
    
    # Create a product instance
    $product = New-Object Product
    $product.Id = [int]$id
    $product.Name = "Sample Product $id"
    $product.Price = 19.99
    $product.Items = @("Item1", "Item2", "Item3")
    
    # Return as XML
    Write-KrXmlResponse -Object $product -StatusCode 200
}

# Add a simple health check endpoint
$server | Add-KrMapRoute -Verbs Get -Pattern '/health' -ScriptBlock {
    Write-KrJsonResponse @{
        status = 'healthy'
        timestamp = (Get-Date).ToString('o')
    } -StatusCode 200
}

# Add endpoint
$server | Add-KrEndpoint -Port 5000

# Start server
Write-Host ''
Write-Host 'Server started successfully!' -ForegroundColor Green
Write-Host ''
Write-Host 'Available endpoints:' -ForegroundColor Cyan
Write-Host '  http://localhost:5000/swagger        - OpenAPI documentation (view XML schema)' -ForegroundColor Yellow
Write-Host '  http://localhost:5000/products/123   - Get product as XML' -ForegroundColor Yellow
Write-Host '  http://localhost:5000/health         - Health check' -ForegroundColor Yellow
Write-Host ''
Write-Host 'XML Schema Features Demonstrated:' -ForegroundColor Cyan
Write-Host '  - Id: XML attribute (not element)' -ForegroundColor White
Write-Host '  - Name: Custom element name (ProductName)' -ForegroundColor White
Write-Host '  - Price: Custom namespace and prefix' -ForegroundColor White
Write-Host '  - Items: Wrapped array elements' -ForegroundColor White
Write-Host ''
Write-Host 'Press ENTER to stop the server...' -ForegroundColor Magenta

$server | Start-KrServer

# Wait for user input
[Console]::ReadLine() | Out-Null

# Stop server
$server | Stop-KrServer
Write-Host 'Server stopped.' -ForegroundColor Yellow
