@{
    FormatVersion = '1.0'
    Name = 'bike-rental-shop-concurrent'
    Description = 'Bike rental shop API with HTTPS, OpenAPI, API key authentication, and a concurrent in-memory database.'
    Version = '1.0.0'
    EntryPoint = './Service.ps1'
    ServiceLogPath = './logs/bike-rental-shop-concurrent.log'
    ApplicationDataFolders = @(
        'data/'
        'logs/'
    )
}
