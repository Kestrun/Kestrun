
<#
.SYNOPSIS
    Safely resolves a schema input to a .NET [Type], supporting generics and arrays.
.DESCRIPTION
    This function takes a schema input that can be either a PowerShell type literal string (e.g., '[System.Collections.Generic.List[System.String]]')
    or a [Type] object, and resolves it to a .NET [Type]. It includes validation to ensure the input is in the correct format and prevents execution of arbitrary code.
.PARAMETER Schema
    The schema input to resolve, either as a PowerShell type literal string or a [Type] object.
.OUTPUTS
    [Type]
.EXAMPLE
    $type = Resolve-KrSchemaTypeLiteral -Schema '[System.Collections.Generic.List[System.String]]'
    This example resolves the schema string to the corresponding .NET [Type] for a list of strings.
.NOTES
    This function is part of the Kestrun PowerShell module for working with OpenAPI specifications
#>
function Resolve-KrSchemaTypeLiteral {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingInvokeExpression', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object] $Schema
    )

    if ($Schema -is [type]) {
        return $Schema
    } elseif ($Schema -is [string]) {
        $s = $Schema.Trim()

        # Require PowerShell type-literal form: [TypeName] or [Namespace.TypeName]
        # Disallow generics, arrays, scripts, whitespace, operators, etc.
        if ($s -notmatch '^\[[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*\]$') {
            throw "Invalid -Schema '$Schema'. Only type literals like '[OpenApiDate]' are allowed."
        }

        # Optional: reject some known-dangerous tokens defensively (belt + suspenders)
        if ($s -match '[\s;|&`$(){}<>]') {
            throw "Invalid -Schema '$Schema'. Disallowed characters detected."
        }

        $Schema = Invoke-Expression $s

        if ($Schema -isnot [type]) {
            throw "Invalid -Schema '$Schema'. Evaluation did not produce a [Type]."
        }
        return $Schema
    } else {
        throw "Invalid -Schema type '$($Schema.GetType().FullName)'. Use ([string]) or 'System.String'."
    }
}
