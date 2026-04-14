Import-Module .\src\PowerShell\Kestrun\Kestrun.psm1 -Force

New-KrServicePackage -SourceFolder .\docs\_includes\examples\pwsh\BikeRentalShop\Synchronized `
    -OutputPath .\artifacts\tutorial\bike-rental-shop.krpack `
    -Force

New-KrServicePackage -SourceFolder .\docs\_includes\examples\pwsh\BikeRentalShop\Concurrent `
    -OutputPath .\artifacts\tutorial\bike-rental-shop-concurrent.krpack `
    -Force

New-KrServicePackage -SourceFolder .\docs\_includes\examples\pwsh\BikeRentalShop\Web `
    -OutputPath .\artifacts\tutorial\bike-rental-shop-web.krpack `
    -Force
