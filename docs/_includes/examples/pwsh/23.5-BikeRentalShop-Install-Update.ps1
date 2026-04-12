Import-Module .\src\PowerShell\Kestrun\Kestrun.psm1 -Force

New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop\Synchronized `
    -OutputPath .\artifacts\tutorial\bike-rental-shop.krpack `
    -Force

dotnet kestrun service help
dotnet kestrun service install --package .\artifacts\tutorial\bike-rental-shop.krpack
dotnet kestrun service update --name bike-rental-shop --package .\artifacts\tutorial\bike-rental-shop-v2.krpack
