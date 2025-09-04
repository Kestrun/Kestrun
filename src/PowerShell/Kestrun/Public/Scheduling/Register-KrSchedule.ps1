<#
    .SYNOPSIS
        Creates a new scheduled job in the active Kestrun host.
    .DESCRIPTION
        Supports either CRON or fixed interval triggers, and either an inline
        ScriptBlock or a script file path.  Use -RunImmediately to execute
        once right after registration.
    .PARAMETER Server
        The Kestrun host instance to use for scheduling the job.
        This is typically the instance running the Kestrun server.
    .PARAMETER Name
        Unique job name.
    .PARAMETER Language
        Script language to use for the job.
    .PARAMETER Cron
        6-field cron expression (seconds precision).  Mutually exclusive with -Interval.
    .PARAMETER Interval
        System.TimeSpan string (e.g. '00:05:00').  Mutually exclusive with -Cron.
    .PARAMETER ScriptBlock
        Inline PowerShell code to run.
        This is the default parameter for the job's script content.
    .PARAMETER Code
        Inline code in the specified language (e.g. C#) to run.
    .PARAMETER ScriptPath
        Path to a .ps1 file. The file is read once at registration time.
    .PARAMETER RunImmediately
        Execute once right after being registered.
    .PARAMETER PassThru
         If specified, the cmdlet will return the newly registered job info.
    .EXAMPLE
        Register-KrSchedule -Name Cache -Interval '00:15:00' -ScriptBlock {
            Clear-KrCache
        }
        Register a job that runs every 15 minutes, clearing the cache.
    .EXAMPLE
        Register-KrSchedule -Name Nightly -Cron '0 0 3 * * *' -ScriptPath 'Scripts/Cleanup.ps1'
        Register a job that runs nightly at 3 AM, executing the script at 'Scripts/Cleanup.ps1'.
    .EXAMPLE
        Register-KrSchedule -Name Heartbeat -Cron '*/10 * * * * *' -ScriptBlock {
            Write-KrInformationLog -Message "💓 Heartbeat at {0:O}" -Values $([DateTimeOffset]::UtcNow)
        }
        Register a job that runs every 10 seconds, logging a heartbeat message.
    .EXAMPLE
        Register-KrSchedule -Name 'InlineJob' -Interval '00:01:00' -ScriptBlock {
            Write-Information "[$([DateTime]::UtcNow.ToString('o'))] 🌙 Inline job ran."
        }
        Register a job that runs every minute, executing the inline PowerShell code.
    .EXAMPLE
        Register-KrSchedule -Server $server -Name 'CSharpJob' -Cron '0 0/5 * * * *' -Language CSharp -Code @"
            Console.WriteLine($"C# job executed at {DateTime.UtcNow:O}");
        "@
        Register a job that runs every 5 minutes, executing inline C# code.
    .EXAMPLE
        Register-KrSchedule -Server $server -Name 'FileJob' -Cron '0 0 1 * * *' -ScriptPath 'Scripts/Backup.cs' -Language CSharp
        Register a job that runs daily at 1 AM, executing the C# script at 'Scripts/Backup.cs'.
    .EXAMPLE
        Register-KrSchedule -Server $server -Name 'RunOnce' -Interval '00:01:00' -ScriptBlock {
            Write-KrInformationLog -Message "Running once at {0:O}" -Values $([DateTimeOffset]::UtcNow)
        } -RunImmediately
        Register a job that runs once immediately after registration, then every minute.
    .EXAMPLE
        Register-KrSchedule -Server $server -Name 'CSharpFileJob' -Cron '0 0 2 * * *' -ScriptPath 'Scripts/ProcessData.cs' -Language CSharp
        Register a job that runs daily at 2 AM, executing the C# script at 'Scripts/ProcessData.cs'.
    .EXAMPLE
        Register-KrSchedule -Server $server -Name 'PythonJob' -Cron '0 0/10 * * * *' -Language Python -ScriptPath 'Scripts/AnalyzeData.py'
        Register a job that runs every 10 minutes, executing the Python script at 'Scripts/AnalyzeData.py'.
    .EXAMPLE
        Register-KrSchedule -Server $server -Name 'JavaScriptJob' -Cron '0 0/15 * * * *' -Language JavaScript -ScriptPath 'Scripts/GenerateReport.js'
        Register a job that runs every 15 minutes, executing the JavaScript script at 'Scripts/GenerateReport.js'.

#>
function Register-KrSchedule {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'IntervalScriptBlock')]
    [OutputType([Kestrun.Scheduling.JobInfo])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'CronFile')]
        [Parameter(Mandatory = $true, ParameterSetName = 'CronBlock')]
        [Parameter(Mandatory = $true, ParameterSetName = 'IntervalBlock')]
        [Parameter(Mandatory = $true, ParameterSetName = 'IntervalFile')]
        [Kestrun.Scripting.ScriptLanguage]$Language,

        [Parameter(Mandatory = $true, ParameterSetName = 'CronScriptBlock')]
        [Parameter(Mandatory = $true, ParameterSetName = 'CronBlock')]
        [Parameter(Mandatory = $true, ParameterSetName = 'CronFile')]
        [string]$Cron,

        [Parameter(Mandatory = $true, ParameterSetName = 'IntervalScriptBlock')]
        [Parameter(Mandatory = $true, ParameterSetName = 'IntervalBlock')]
        [Parameter(Mandatory = $true, ParameterSetName = 'IntervalFile')]
        [timespan]$Interval,

        [Parameter(Mandatory = $true, ParameterSetName = 'CronScriptBlock')]
        [Parameter(Mandatory = $true, ParameterSetName = 'IntervalScriptBlock')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'CronBlock')]
        [Parameter(Mandatory = $true, ParameterSetName = 'IntervalBlock')]
        [string]$Code,

        [Parameter(Mandatory = $true, ParameterSetName = 'CronFile')]
        [Parameter(Mandatory = $true, ParameterSetName = 'IntervalFile')]
        [string]$ScriptPath,

        [Parameter()]
        [switch]$RunImmediately,

        [Parameter()]
        [switch]$PassThru
    )

    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        # Ensure the scheduler service is enabled
        $sched = $Server.Scheduler
        if ($null -eq $sched) {
            throw 'SchedulerService is not enabled. Call host.EnableScheduling() first.'
        }

        try {
            switch ($PSCmdlet.ParameterSetName) {

                'CronScriptBlock' {
                    $sched.Schedule($Name, $Cron, $ScriptBlock, $RunImmediately.IsPresent)
                }
                'IntervalScriptBlock' {
                    $sched.Schedule($Name, $Interval, $ScriptBlock, $RunImmediately.IsPresent)
                }
                'CronBlock' {
                    $sched.Schedule($Name, $Cron, $Code, $Language, $RunImmediately.IsPresent)
                }
                'IntervalBlock' {
                    $sched.Schedule($Name, $Interval, $Code, $Language, $RunImmediately.IsPresent)
                }
                'CronFile' {
                    $fileInfo = [System.IO.FileInfo]$ScriptPath
                    if (-not $fileInfo.Exists) {
                        throw "Script file '$ScriptPath' does not exist."
                    }
                    $sched.Schedule($Name, $Cron, $fileInfo, $Language, $RunImmediately.IsPresent)
                }
                'IntervalFile' {
                    $fileInfo = [System.IO.FileInfo]$ScriptPath
                    if (-not $fileInfo.Exists) {
                        throw "Script file '$ScriptPath' does not exist."
                    }
                    $sched.Schedule($Name, $Interval, $fileInfo, $Language, $RunImmediately.IsPresent)
                }
            }
            if ($PassThru.IsPresent) {
                # if the PassThru switch is specified, return the job info
                # Return the newly registered job info
                Write-KrInformationLog -Message "Schedule '{0}' registered successfully." -Values $Name

                # return the freshly-registered JobInfo
                return $sched.GetSnapshot() | Where-Object Name -EQ $Name
            } else {
                Write-KrInformationLog -Message "Schedule '{0}' registered successfully. Use -PassThru to return the job info." -Values $Name
            }
        } catch {
            Write-KrErrorLog -Message 'Failed to register schedule' -ErrorRecord $_
        }
    }
}

