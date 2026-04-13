$clientConfig = [ordered]@{
    apiBaseUrl = $BikeRentalApiBaseUrl
    publicCatalogPath = '/api/bikes?status=all'
    createRentalPath = '/api/rentals'
    rentalStatusBasePath = '/api/rentals'
    healthPath = '/api/shop-health'
    staffDashboardPath = '/api/staff/dashboard'
    addBikePath = '/api/staff/bikes'
    removeBikeBasePath = '/api/staff/bikes'
    returnRentalBasePath = '/api/staff/rentals'
    demoApiKey = $StaffApiKey
}

$Model = [pscustomobject]@{
    Title = 'Riverside Bike Rental Web'
    Description = 'A standalone booking studio for the live bike-rental API.'
    PageKey = 'home'
    Badge = 'Standalone web client'
    Headline = 'Browse live inventory in one service while the bike-rental API runs as its own backend.'
    Lead = 'This browser client talks directly to the configured bike-rental API, so you can switch between the synchronized and concurrent backends without redesigning the UI.'
    Highlights = @('Separate web service', 'Same API contract', 'Backend swap without UI rewrites')
    DefaultBikeId = 'bk-100'
    DefaultPhone = '+1-202-555-0148'
    DefaultPlannedHours = 3
    OperationsUrl = '/Operations'
    BackendLabel = $BikeRentalBackendLabel
    BackendUrl = $BikeRentalApiBaseUrl
    BackendDocsUrl = $BikeRentalBackendDocsUrl
    BackendOpenApiUrl = $BikeRentalBackendOpenApiUrl
    ClientConfigJson = $clientConfig | ConvertTo-Json -Depth 8 -Compress
}
