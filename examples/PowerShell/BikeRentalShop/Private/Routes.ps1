function global:New-BikeCatalogItemObject {
    param([hashtable]$Bike)

    return [BikeCatalogItem]@{
        bikeId = [string]$Bike['bikeId']
        model = [string]$Bike['model']
        type = [BikeType]$Bike['type']
        hourlyRate = [double]$Bike['hourlyRate']
        status = [string]$Bike['status']
        dock = [string]$Bike['dock']
    }
}

function global:New-RentalStatusObject {
    param(
        [hashtable]$Rental,
        [hashtable]$Bike
    )

    return [RentalStatusResponse]@{
        rentalId = [string]$Rental['rentalId']
        bikeId = [string]$Rental['bikeId']
        customerName = [string]$Rental['customerName']
        bikeModel = [string]$Bike['model']
        status = [string]$Rental['status']
        pickupCode = [string]$Rental['pickupCode']
        startedAtUtc = [string]$Rental['startedAtUtc']
        dueAtUtc = [string]$Rental['dueAtUtc']
        returnedAtUtc = [string]$Rental['returnedAtUtc']
        totalEstimate = [double]$Rental['totalEstimate']
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/' -AllowAnonymous -ScriptBlock {
    Write-KrJsonResponse -InputObject ([ordered]@{
        service = 'Riverside Bike Rental'
        openApi = '/openapi/v3.1/openapi.json'
        docs = '/docs/swagger'
        publicEndpoints = @('/api/bikes', '/api/rentals', '/api/rentals/{rentalId}', '/api/shop-health')
        staffEndpoints = @('/api/staff/dashboard', '/api/staff/bikes', '/api/staff/bikes/{bikeId}', '/api/staff/rentals/{rentalId}/return')
        demoApiKeyHeader = 'X-Api-Key: bike-shop-demo-key'
        packageCommand = 'New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop -OutputPath .\bike-rental-shop-1.0.0.krpack'
    }) -StatusCode 200
}

<#
.SYNOPSIS
    List bikes in the rental catalog.
.DESCRIPTION
    Returns the current bike inventory and supports filtering by availability status and bike type.
.PARAMETER status
    Availability filter for the bike catalog. Use all to return every bike regardless of status.
.PARAMETER type
    Optional bike category filter that narrows the catalog to a single rental type.
#>
function listBikes {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/bikes', Tags = 'Catalog')]
    [OpenApiResponse(StatusCode = '200', Description = 'Bike catalog returned.', Schema = [BikeCatalogResponse])]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Query, Example = 'available')]
        [ValidateSet('all', 'available', 'rented')]
        [string]$status = 'available',

        [OpenApiParameter(In = [OaParameterLocation]::Query, Example = 'city')]
        [Nullable[BikeType]]$type
    )

    $state = Get-BikeRentalState
    $bikes = @($state['bikes'])

    if ($status -ne 'all') {
        $bikes = @($bikes | Where-Object { [string]$_['status'] -eq $status })
    }

    if ($null -ne $type) {
        $selectedType = $type.ToString()
        $bikes = @($bikes | Where-Object { [string]$_['type'] -eq $selectedType })
    }

    $response = foreach ($bike in $bikes) {
        New-BikeCatalogItemObject -Bike $bike
    }

    Write-KrJsonResponse -InputObject @($response) -StatusCode 200
}

<#
.SYNOPSIS
    Create a new rental booking.
.DESCRIPTION
    Starts a rental for an available bike, assigns a pickup code, and records the reservation in persisted state.
.PARAMETER Body
    Rental request payload containing the bike selection, customer identity, contact phone, and planned rental hours.
#>
function createRental {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/rentals', Tags = 'Rentals')]
    [OpenApiResponse(StatusCode = '201', Description = 'Rental created.', Schema = [RentalStatusResponse])]
    [OpenApiResponse(StatusCode = '404', Description = 'Bike was not found.')]
    [OpenApiResponse(StatusCode = '409', Description = 'Bike is not available.')]
    param(
        [OpenApiRequestBody(Required = $true, ContentType = 'application/json')]
        [CreateRentalRequest]$Body
    )

    $result = Update-BikeRentalState {
        param([hashtable]$State)

        $bike = @($State['bikes']) | Where-Object { [string]$_['bikeId'] -eq $Body.bikeId } | Select-Object -First 1
        if ($null -eq $bike) {
            return [ordered]@{
                StatusCode = 404
                Payload = [ordered]@{
                    error = 'Bike not found.'
                    bikeId = $Body.bikeId
                }
            }
        }

        if ([string]$bike['status'] -ne 'available') {
            return [ordered]@{
                StatusCode = 409
                Payload = [ordered]@{
                    error = 'Bike is not available for checkout.'
                    bikeId = $Body.bikeId
                    currentStatus = [string]$bike['status']
                }
            }
        }

        $now = (Get-Date).ToUniversalTime()
        $due = $now.AddHours($Body.plannedHours)
        $rentalId = 'rent-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
        $pickupCode = (Get-Random -Minimum 100000 -Maximum 999999).ToString()
        $estimate = [Math]::Round(([double]$bike['hourlyRate'] * $Body.plannedHours), 2)

        $rental = [ordered]@{
            rentalId = $rentalId
            bikeId = [string]$bike['bikeId']
            customerName = [string]$Body.customerName
            phone = [string]$Body.phone
            plannedHours = [int]$Body.plannedHours
            startedAtUtc = $now.ToString('o')
            dueAtUtc = $due.ToString('o')
            returnedAtUtc = $null
            pickupCode = $pickupCode
            status = 'active'
            totalEstimate = $estimate
        }

        $bike['status'] = 'rented'
        $bike['currentRentalId'] = $rentalId
        $State['rentals'] = @($State['rentals']) + $rental

        Write-KrLog -Level Information -Message 'Created rental {rentalId} for bike {bikeId}.' -Values $rentalId, $bike['bikeId']

        return [ordered]@{
            StatusCode = 201
            Payload = New-RentalStatusObject -Rental $rental -Bike $bike
        }
    }

    if ($result['StatusCode'] -ne 201) {
        Write-BikeRentalError -StatusCode $result['StatusCode'] -Message $result['Payload']['error'] -Details $result['Payload']
        return
    }

    Write-KrJsonResponse -InputObject $result['Payload'] -StatusCode 201
}

<#
.SYNOPSIS
    Get the status of a rental.
.DESCRIPTION
    Looks up a rental by identifier and returns its current lifecycle state together with the assigned bike details.
.PARAMETER rentalId
    Rental identifier returned when the booking was created.
#>
function getRentalStatus {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/rentals/{rentalId}', Tags = 'Rentals')]
    [OpenApiResponse(StatusCode = '200', Description = 'Rental found.', Schema = [RentalStatusResponse])]
    [OpenApiResponse(StatusCode = '404', Description = 'Rental was not found.')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Example = 'rent-6f7d2cae8a8b')]
        [string]$rentalId
    )

    $state = Get-BikeRentalState
    $rental = @($state['rentals']) | Where-Object { [string]$_['rentalId'] -eq $rentalId } | Select-Object -First 1
    if ($null -eq $rental) {
        Write-BikeRentalError -StatusCode 404 -Message 'Rental not found.' -Details @{ rentalId = $rentalId }
        return
    }

    $bike = @($state['bikes']) | Where-Object { [string]$_['bikeId'] -eq [string]$rental['bikeId'] } | Select-Object -First 1
    Write-KrJsonResponse -InputObject (New-RentalStatusObject -Rental $rental -Bike $bike) -StatusCode 200
}

<#
.SYNOPSIS
    Report shop API health.
.DESCRIPTION
    Returns a lightweight operational snapshot including shop name, active rentals, and available bike count.
#>
function getBikeRentalHealth {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/shop-health', Tags = 'Operations')]
    [OpenApiResponse(StatusCode = '200', Description = 'Health response returned.', Schema = [BikeRentalHealthResponse])]
    param()

    $state = Get-BikeRentalState
    $availableBikes = @($state['bikes'] | Where-Object { [string]$_['status'] -eq 'available' }).Count
    $activeRentals = @($state['rentals'] | Where-Object { [string]$_['status'] -eq 'active' }).Count

    Write-KrJsonResponse -InputObject ([BikeRentalHealthResponse]@{
        status = 'healthy'
        shopName = [string]$state['shopName']
        availableBikes = $availableBikes
        activeRentals = $activeRentals
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }) -StatusCode 200
}

<#
.SYNOPSIS
    Get the authenticated staff dashboard.
.DESCRIPTION
    Returns the internal operations dashboard with inventory totals, active rentals, and same-day completion counts.
#>
function getStaffDashboard {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/staff/dashboard', Tags = 'Staff')]
    [OpenApiResponseRef(StatusCode = '200', ReferenceId = 'StaffDashboardOk')]
    [OpenApiAuthorization(Scheme = 'BikeRentalStaffApiKey')]
    param()

    $state = Get-BikeRentalState
    $availableCount = @($state['bikes'] | Where-Object { [string]$_['status'] -eq 'available' }).Count
    $rentedCount = @($state['bikes'] | Where-Object { [string]$_['status'] -eq 'rented' }).Count
    $activeRentals = @($state['rentals'] | Where-Object { [string]$_['status'] -eq 'active' })
    $completedToday = @($state['rentals'] | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_['returnedAtUtc']) -and
        ([DateTimeOffset]::Parse([string]$_['returnedAtUtc']).UtcDateTime.Date -eq [DateTime]::UtcNow.Date)
    }).Count

    Write-KrJsonResponse -InputObject ([StaffDashboardResponse]@{
        shopName = [string]$state['shopName']
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        inventory = [StaffDashboardInventoryResponse]@{
            total = @($state['bikes']).Count
            available = $availableCount
            rented = $rentedCount
        }
        rentals = [StaffDashboardRentalsResponse]@{
            active = @($activeRentals).Count
            completedToday = $completedToday
            activeIds = @($activeRentals | ForEach-Object { [string]$_['rentalId'] })
        }
    }) -StatusCode 200
}

<#
.SYNOPSIS
    Add a bike to staff inventory.
.DESCRIPTION
    Creates a new inventory entry for a bike, marks it as available, and persists the updated catalog for future rentals.
.PARAMETER Body
    Bike definition containing the identifier, model, type, hourly rate, dock location, and optional last service date.
#>
function addBike {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/staff/bikes', Tags = 'Staff')]
    [OpenApiResponseRef(StatusCode = '201', ReferenceId = 'StaffAddBikeCreated')]
    [OpenApiResponseRef(StatusCode = '400', ReferenceId = 'StaffAddBikeBadRequest')]
    [OpenApiResponseRef(StatusCode = '409', ReferenceId = 'StaffAddBikeConflict')]
    [OpenApiAuthorization(Scheme = 'BikeRentalStaffApiKey')]
    param(
        [OpenApiRequestBody(Required = $true, ContentType = 'application/json')]
        [AddBikeRequest]$Body
    )

    if (-not [string]::IsNullOrWhiteSpace($Body.lastServiceDate)) {
        $parsedDate = [datetime]::MinValue
        if (-not [datetime]::TryParseExact([string]$Body.lastServiceDate, 'yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$parsedDate)) {
            Write-BikeRentalError -StatusCode 400 -Message 'Last service date must use yyyy-MM-dd format.' -Details @{ bikeId = $Body.bikeId }
            return
        }
    }

    $result = Update-BikeRentalState {
        param([hashtable]$State)

        $existingBike = @($State['bikes']) | Where-Object { [string]$_['bikeId'] -eq $Body.bikeId } | Select-Object -First 1
        if ($null -ne $existingBike) {
            return [ordered]@{
                StatusCode = 409
                Payload = [ordered]@{
                    error = 'Bike identifier already exists.'
                    bikeId = $Body.bikeId
                    currentStatus = [string]$existingBike['status']
                }
            }
        }

        $lastServiceDate = if ([string]::IsNullOrWhiteSpace($Body.lastServiceDate)) {
            (Get-Date).ToString('yyyy-MM-dd')
        } else {
            [string]$Body.lastServiceDate
        }

        $bike = [ordered]@{
            bikeId = [string]$Body.bikeId
            model = [string]$Body.model
            type = $Body.type.ToString()
            hourlyRate = [double]$Body.hourlyRate
            status = 'available'
            dock = [string]$Body.dock
            lastServiceDate = $lastServiceDate
            currentRentalId = $null
        }

        $State['bikes'] = @($State['bikes']) + $bike

        Write-KrLog -Level Information -Message 'Added bike {bikeId} to inventory.' -Values $bike['bikeId']

        return [ordered]@{
            StatusCode = 201
            Payload = New-BikeCatalogItemObject -Bike $bike
        }
    }

    if ($result['StatusCode'] -ne 201) {
        Write-BikeRentalError -StatusCode $result['StatusCode'] -Message $result['Payload']['error'] -Details $result['Payload']
        return
    }

    Write-KrJsonResponse -InputObject $result['Payload'] -StatusCode 201
}

<#
.SYNOPSIS
    Remove a bike from staff inventory.
.DESCRIPTION
    Deletes a bike from the persisted catalog when it exists and is not currently rented.
.PARAMETER bikeId
    Bike identifier for the inventory entry to remove.
#>
function removeBike {
    [OpenApiPath(HttpVerb = 'delete', Pattern = '/api/staff/bikes/{bikeId}', Tags = 'Staff')]
    [OpenApiResponseRef(StatusCode = '200', ReferenceId = 'StaffRemoveBikeOk')]
    [OpenApiResponseRef(StatusCode = '404', ReferenceId = 'StaffRemoveBikeNotFound')]
    [OpenApiResponseRef(StatusCode = '409', ReferenceId = 'StaffRemoveBikeConflict')]
    [OpenApiAuthorization(Scheme = 'BikeRentalStaffApiKey')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Example = 'bk-550')]
        [string]$bikeId
    )

    $result = Update-BikeRentalState {
        param([hashtable]$State)

        $bikes = @($State['bikes'])
        $bike = $bikes | Where-Object { [string]$_['bikeId'] -eq $bikeId } | Select-Object -First 1
        if ($null -eq $bike) {
            return [ordered]@{
                StatusCode = 404
                Payload = [ordered]@{
                    error = 'Bike not found.'
                    bikeId = $bikeId
                }
            }
        }

        if ([string]$bike['status'] -eq 'rented') {
            return [ordered]@{
                StatusCode = 409
                Payload = [ordered]@{
                    error = 'Bike cannot be removed while it is rented.'
                    bikeId = $bikeId
                    currentStatus = [string]$bike['status']
                }
            }
        }

        $State['bikes'] = @($bikes | Where-Object { [string]$_['bikeId'] -ne $bikeId })

        Write-KrLog -Level Information -Message 'Removed bike {bikeId} from inventory.' -Values $bikeId

        return [ordered]@{
            StatusCode = 200
            Payload = New-BikeCatalogItemObject -Bike $bike
        }
    }

    if ($result['StatusCode'] -ne 200) {
        Write-BikeRentalError -StatusCode $result['StatusCode'] -Message $result['Payload']['error'] -Details $result['Payload']
        return
    }

    Write-KrJsonResponse -InputObject $result['Payload'] -StatusCode 200
}

<#
.SYNOPSIS
    Return a rented bike to inventory.
.DESCRIPTION
    Closes an active rental, marks the bike as available again, and optionally stores staff condition notes.
.PARAMETER rentalId
    Rental identifier for the booking being closed.
.PARAMETER Body
    Optional return payload with staff condition notes captured during check-in.
#>
function returnRental {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/staff/rentals/{rentalId}/return', Tags = 'Staff')]
    [OpenApiResponseRef(StatusCode = '200', ReferenceId = 'StaffReturnRentalOk')]
    [OpenApiResponseRef(StatusCode = '404', ReferenceId = 'StaffReturnRentalNotFound')]
    [OpenApiResponseRef(StatusCode = '409', ReferenceId = 'StaffReturnRentalConflict')]
    [OpenApiAuthorization(Scheme = 'BikeRentalStaffApiKey')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Example = 'rent-6f7d2cae8a8b')]
        [string]$rentalId,

        [OpenApiRequestBody(Required = $false, ContentType = 'application/json')]
        [CloseRentalRequest]$Body
    )

    $conditionNotes = if ($null -ne $Body) { [string]$Body.conditionNotes } else { [string]$null }

    $result = Update-BikeRentalState {
        param([hashtable]$State)

        $rental = @($State['rentals']) | Where-Object { [string]$_['rentalId'] -eq $rentalId } | Select-Object -First 1
        if ($null -eq $rental) {
            return [ordered]@{
                StatusCode = 404
                Payload = [ordered]@{
                    error = 'Rental not found.'
                    rentalId = $rentalId
                }
            }
        }

        if ([string]$rental['status'] -ne 'active') {
            return [ordered]@{
                StatusCode = 409
                Payload = [ordered]@{
                    error = 'Rental is already closed.'
                    rentalId = $rentalId
                    currentStatus = [string]$rental['status']
                }
            }
        }

        $bike = @($State['bikes']) | Where-Object { [string]$_['bikeId'] -eq [string]$rental['bikeId'] } | Select-Object -First 1
        $returnedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        $rental['status'] = 'returned'
        $rental['returnedAtUtc'] = $returnedAtUtc
        $rental['conditionNotes'] = $conditionNotes

        if ($null -ne $bike) {
            $bike['status'] = 'available'
            $bike['currentRentalId'] = $null
        }

        Write-KrLog -Level Information -Message 'Closed rental {rentalId} for bike {bikeId}.' -Values $rentalId, $rental['bikeId']

        return [ordered]@{
            StatusCode = 200
            Payload = New-RentalStatusObject -Rental $rental -Bike $bike
        }
    }

    if ($result['StatusCode'] -ne 200) {
        Write-BikeRentalError -StatusCode $result['StatusCode'] -Message $result['Payload']['error'] -Details $result['Payload']
        return
    }

    Write-KrJsonResponse -InputObject $result['Payload'] -StatusCode 200
}
