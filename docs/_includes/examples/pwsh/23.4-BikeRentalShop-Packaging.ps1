Import-Module .\src\PowerShell\Kestrun\Kestrun.psm1 -Force

New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop\Synchronized `
    -OutputPath .\artifacts\tutorial\bike-rental-shop.krpack `
    -Force

New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop\Concurrent `
    -OutputPath .\artifacts\tutorial\bike-rental-shop-concurrent.krpack `
    -Force

New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop\Web `
    -OutputPath .\artifacts\tutorial\bike-rental-shop-web.krpack `
    -Force
