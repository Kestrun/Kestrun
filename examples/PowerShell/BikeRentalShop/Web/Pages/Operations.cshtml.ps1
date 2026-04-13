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
    Title = 'Riverside Operations Deck'
    Description = 'A standalone staff console for the protected bike-rental API.'
    PageKey = 'operations'
    Badge = 'Staff web console'
    Headline = 'Monitor the floor, cycle inventory, and close rentals while the backend runs independently.'
    Lead = 'The controls on this page target the configured backend API with the demo staff key, which keeps the service boundaries clear while still exercising the protected routes.'
    Checkpoints = @('Dashboard stats refresh live', 'Add or remove inventory', 'Close active rentals with notes')
    HomeUrl = '/'
    BackendLabel = $BikeRentalBackendLabel
    BackendUrl = $BikeRentalApiBaseUrl
    BackendDocsUrl = $BikeRentalBackendDocsUrl
    BackendOpenApiUrl = $BikeRentalBackendOpenApiUrl
    ClientConfigJson = $clientConfig | ConvertTo-Json -Depth 8 -Compress
}
