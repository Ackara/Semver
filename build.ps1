<#
.SYNOPSIS
A psake bootstraper; This script runs one or more tasks defined in the psake file.

.EXAMPLE
.\build.ps1 -Help;
This example prints a list of all the available tasks.
#>

Param(
	[ValidateNotNullorEmpty()]
	[string[]]$Tasks = @("default"),

	[Alias('f', "desc")]
	[ValidateNotNullorEmpty()]
	[string]$Filter = "*",

	[Alias("secrets")]
	[string]$SecretsFilePath,

	[Alias('no-commit')]
	[switch]$SkipCommit,

	[Alias('h', '?')]
	[switch]$Help,

	[Alias('d', "dry")]
	[switch]$DryRun,

	[switch]$Release,
	[switch]$Major,
	[switch]$Minor,
	[switch]$Force
)
# Validate Dependencies
if(-not ((&node --version) -match 'v\d+.\d+')) { throw "'nodejs' is not accessible on this machine."; }
if(-not ((&dotnet --version) -match '\d+.\d+')) { throw "'dotnet' is not accessible on this machine."; }
if (-not ((&git --version) -match 'git version \d+\.\d+')) { throw "'git' is not accessible on this machine."; }

# Initializing required variables.
$Configuration = "Debug";
if ($Release) { $Configuration = "Release"; }

$SecretsFilePath = (Join-Path $PSScriptRoot "secrets.json");

# Getting the current branch of source control.
$branchName = ([Environment]::GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME"));
if ([string]::IsNullOrEmpty($branchName))
{
	$match = [Regex]::Match((& git branch), '\*\s*(?<name>\w+)');
	if ($match.Success) { $branchName = $match.Groups["name"].Value; }
}

# Installing then invoking the Psake tasks.
$toolsFolder = Join-Path $PSScriptRoot "tools";
$psakeModule = Join-Path $toolsFolder "psake/*/*.psd1";
if (-not (Test-Path $psakeModule))
{
	if (-not (Test-Path $toolsFolder)) { New-Item $toolsFolder -ItemType Directory | Out-Null; }
	Save-Module "psake" -Path $toolsFolder;
}
Import-Module $psakeModule -Force;

$taskFile = Join-Path $PSScriptRoot "build/tasks.psake.ps1";
if ($Help) { Invoke-Psake -buildFile $taskFile -docs; }
else
{
	Write-Host -ForegroundColor DarkGray "User:          $([Environment]::UserName)@$([Environment]::MachineName)";
	Write-Host -ForegroundColor DarkGray "Platform:      $([Environment]::OSVersion.Platform)";
	Write-Host -ForegroundColor DarkGray "Branch:        $branchName";
	Write-Host -ForegroundColor DarkGray "Configuration: $Configuration";
	Write-Host "";
	Invoke-psake $taskFile -nologo -taskList $Tasks -properties @{
		"Filter"=$Filter;
		"Major"=$Major.IsPresent;
		"Minor"=$Minor.IsPresent;
		"Force"=$Force.IsPresent;
		"DryRun"=$DryRun.IsPresent;
		"ToolsFolder"=$toolsFolder;
		"CurrentBranch"=$branchName;
		"Configuration"=$Configuration;
		"SolutionFolder"=$PSScriptRoot;
		"SecretsFilePath"=$SecretsFilePath;
		"ShouldCommitChanges"=(-not $SkipCommit.IsPresent);
	}
	if (-not $psake.build_success) { exit 1; }
}