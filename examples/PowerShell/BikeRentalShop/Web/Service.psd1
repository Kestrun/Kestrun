@{
    FormatVersion = '1.0'
    Name = 'bike-rental-shop-web'
    Description = 'Standalone Razor Pages web client for the bike rental shop backends.'
    Version = '1.0.0'
    EntryPoint = './Service.ps1'
    ServiceLogPath = './logs/bike-rental-shop-web.log'
    ApplicationDataFolders = @(
        'data/'
        'logs/'
    )
}
