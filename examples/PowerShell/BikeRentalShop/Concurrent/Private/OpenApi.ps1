<#
.SYNOPSIS
    Declares the OpenAPI schema and response components for the concurrent bike rental shop service.
.DESCRIPTION
    This script defines the reusable PowerShell classes, enums, and response components used to
    generate the OpenAPI document for the concurrent bike rental sample. The route definitions in
    Routes.ps1 reference these components to keep the API contract explicit and reusable.
.EXAMPLE
    The declarations in this file are dot-sourced by Service.ps1 before routes are registered so the
    OpenAPI annotation scanner can discover them.
#>
enum BikeType {
    city
    mountain
    hybrid
    electric
}

[OpenApiSchemaComponent(Description = 'Bike available in the rental catalog.', RequiredProperties = ('bikeId', 'model', 'type', 'hourlyRate', 'status', 'dock'))]
class BikeCatalogItem {
    [OpenApiProperty(Description = 'Unique bike identifier.', Example = 'bk-100')]
    [string]$bikeId

    [OpenApiProperty(Description = 'Display model for the bike.', Example = 'City Loop 3')]
    [string]$model

    [OpenApiProperty(Description = 'Rental category.', Example = 'city')]
    [BikeType]$type

    [OpenApiProperty(Description = 'Hourly rental rate in USD.', Example = 12.0)]
    [double]$hourlyRate

    [OpenApiProperty(Description = 'Current availability status.', Example = 'available')]
    [string]$status

    [OpenApiProperty(Description = 'Current storage dock in the shop.', Example = 'front-window')]
    [string]$dock
}

[OpenApiSchemaComponent(Array = $true, Description = 'List of bikes currently tracked by the shop.')]
class BikeCatalogResponse : BikeCatalogItem {}

[OpenApiSchemaComponent(Description = 'Create a new bike rental.', RequiredProperties = ('bikeId', 'customerName', 'plannedHours'))]
class CreateRentalRequest {
    [OpenApiProperty(Description = 'Bike identifier to rent.', Example = 'bk-100')]
    [string]$bikeId

    [OpenApiProperty(Description = 'Name of the rider or customer.', Example = 'Ava Flores')]
    [string]$customerName

    [OpenApiProperty(Description = 'Contact phone number for pickup coordination.', Example = '+1-202-555-0148')]
    [string]$phone

    [OpenApiProperty(Description = 'Planned rental duration in hours.', Example = 3)]
    [ValidateRange(1, 24)]
    [int]$plannedHours
}

[OpenApiSchemaComponent(
    Description = 'Confirmation returned after a rental is created.',
    RequiredProperties = ('rentalId', 'bikeId', 'customerName', 'bikeModel', 'status', 'pickupCode', 'startedAtUtc', 'dueAtUtc', 'totalEstimate')
)]
class RentalStatusResponse {
    [OpenApiProperty(Description = 'Rental identifier.', Example = 'rent-6f7d2cae8a8b')]
    [string]$rentalId

    [OpenApiProperty(Description = 'Bike identifier assigned to the rental.', Example = 'bk-100')]
    [string]$bikeId

    [OpenApiProperty(Description = 'Customer display name.', Example = 'Ava Flores')]
    [string]$customerName

    [OpenApiProperty(Description = 'Model name for the rented bike.', Example = 'City Loop 3')]
    [string]$bikeModel

    [OpenApiProperty(Description = 'Rental lifecycle status.', Example = 'active')]
    [string]$status

    [OpenApiProperty(Description = 'Pickup verification code used at the counter.', Example = '418203')]
    [string]$pickupCode

    [OpenApiProperty(Description = 'Rental start time in UTC.', Example = '2026-04-06T15:45:00.0000000Z')]
    [string]$startedAtUtc

    [OpenApiProperty(Description = 'Planned return time in UTC.', Example = '2026-04-06T18:45:00.0000000Z')]
    [string]$dueAtUtc

    [OpenApiProperty(Description = 'Actual return time in UTC when completed.', Example = '2026-04-06T18:31:00.0000000Z')]
    [string]$returnedAtUtc

    [OpenApiProperty(Description = 'Estimated charge for the rental in USD.', Example = 36.0)]
    [double]$totalEstimate
}

[OpenApiSchemaComponent(Description = 'Health snapshot for the bike rental API.', RequiredProperties = ('status', 'shopName', 'availableBikes', 'activeRentals', 'timestamp'))]
class BikeRentalHealthResponse {
    [string]$status
    [string]$shopName
    [int]$availableBikes
    [int]$activeRentals
    [string]$timestamp
}

[OpenApiSchemaComponent(Description = 'Inventory totals shown on the staff dashboard.', RequiredProperties = ('total', 'available', 'rented'))]
class StaffDashboardInventoryResponse {
    [int]$total
    [int]$available
    [int]$rented
}

[OpenApiSchemaComponent(Description = 'Rental summary shown on the staff dashboard.', RequiredProperties = ('active', 'completedToday', 'activeIds'))]
class StaffDashboardRentalsResponse {
    [int]$active
    [int]$completedToday
    [string[]]$activeIds
}

[OpenApiSchemaComponent(Description = 'Authenticated staff dashboard snapshot.', RequiredProperties = ('shopName', 'generatedAtUtc', 'inventory', 'rentals'))]
class StaffDashboardResponse {
    [string]$shopName
    [string]$generatedAtUtc
    [StaffDashboardInventoryResponse]$inventory
    [StaffDashboardRentalsResponse]$rentals
}

[OpenApiSchemaComponent(Description = 'Optional details captured when a bike is returned.')]
class CloseRentalRequest {
    [OpenApiProperty(Description = 'Optional condition or maintenance notes recorded by staff.', Example = 'Rear tire pressure checked and ready for the next rider.')]
    [string]$conditionNotes
}

[OpenApiSchemaComponent(Description = 'Request payload used to add a bike to shop inventory.', RequiredProperties = ('bikeId', 'model', 'type', 'hourlyRate', 'dock'))]
class AddBikeRequest {
    [OpenApiProperty(Description = 'Unique bike identifier.', Example = 'bk-550')]
    [ValidatePattern('^bk-\d{3,}$')]
    [string]$bikeId

    [OpenApiProperty(Description = 'Display model for the bike.', Example = 'Harbor Cruiser 7')]
    [ValidateNotNullOrEmpty()]
    [ValidateLength(3, 80)]
    [string]$model

    [OpenApiProperty(Description = 'Rental category.', Example = 'city')]
    [BikeType]$type

    [OpenApiProperty(Description = 'Hourly rental rate in USD.', Example = 14.5)]
    [ValidateRange(0.01, 500.0)]
    [double]$hourlyRate

    [OpenApiProperty(Description = 'Storage dock or rack location.', Example = 'south-rack')]
    [ValidateNotNullOrEmpty()]
    [ValidateLength(2, 60)]
    [string]$dock

    [OpenApiProperty(Description = 'Last service date in ISO 8601 date format.', Example = '2026-04-06')]
    [string]$lastServiceDate
}

[OpenApiSchemaComponent(Description = 'Standard API error returned by the bike rental service.', RequiredProperties = ('status', 'error', 'path', 'timestamp'))]
class BikeRentalErrorResponse {
    [int]$status
    [string]$error
    [string]$path
    [string]$timestamp
    [string]$bikeId
    [string]$rentalId
    [string]$currentStatus
}

[OpenApiResponseComponent(Description = 'Authenticated staff dashboard returned.', ContentType = 'application/json')]
[StaffDashboardResponse]$StaffDashboardOk = NoDefault

[OpenApiResponseComponent(Description = 'Rental closed and bike returned to inventory.', ContentType = 'application/json')]
[RentalStatusResponse]$StaffReturnRentalOk = NoDefault

[OpenApiResponseComponent(Description = 'Rental was not found.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffReturnRentalNotFound = NoDefault

[OpenApiResponseComponent(Description = 'Rental is already closed.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffReturnRentalConflict = NoDefault

[OpenApiResponseComponent(Description = 'Bike added to inventory.', ContentType = 'application/json')]
[BikeCatalogItem]$StaffAddBikeCreated = NoDefault

[OpenApiResponseComponent(Description = 'Bike payload is invalid.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffAddBikeBadRequest = NoDefault

[OpenApiResponseComponent(Description = 'Bike identifier already exists.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffAddBikeConflict = NoDefault

[OpenApiResponseComponent(Description = 'Bike removed from inventory.', ContentType = 'application/json')]
[BikeCatalogItem]$StaffRemoveBikeOk = NoDefault

[OpenApiResponseComponent(Description = 'Bike was not found.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffRemoveBikeNotFound = NoDefault

[OpenApiResponseComponent(Description = 'Bike cannot be removed while it is rented.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffRemoveBikeConflict = NoDefault
