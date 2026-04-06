@{
    FormatVersion = '1.0'
    Name = 'bike-rental-shop'
    Description = 'Bike rental shop API with HTTPS, OpenAPI, API key authentication, and persistent data.'
    Version = '1.0.0'
    EntryPoint = './Service.ps1'
    ServiceLogPath = './logs/bike-rental-shop.log'
    ApplicationDataFolders = @(
        'data/'
        'logs/'
    )
}
