function Get-BuildCredential
{
	Param(
		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[ValidateScript({Test-Path $_ -PathType Leaf})]
		[string]$SecretsFilePath,

		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$Configuration,

		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$CurrentBranch
	)

	$key = $CurrentBranch;
	$secrets = Get-Content $SecretsFilePath | ConvertFrom-Json;
	if ($Configuration -eq "Debug") { $key = "local"; }

	foreach ($item in @($key, "preview"))
	{
		$obj = Select-Property $item $secrets;
		if ($obj -ne $null) { return $obj; }
	}

	throw "The '$CurrentBranch' does not any credentials configured.";
}

function Get-MSBuildElement
{
	Param(
		[Parameter(Mandatory)]
		[ValidateScript({Test-Path $_})]
		[string]$ProjectFile,

		[Parameter(Mandatory)]
		[string]$XPath
	)

	[xml]$proj = Get-Content $ProjectFile;
	$ns = [System.Xml.XmlNamespaceManager]::new($proj.NameTable);
	$ns.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

	return $proj.SelectSingleNode($XPath, $ns);
}

function Get-HostConfiguration([string]$name, [string]$currentBranch)
{
	foreach ($branch in @($currentBranch, "preview"))
	{
		[string]$path = Join-Path $PSScriptRoot "profiles/$name-$branch.*" | Resolve-Path;
		if (($path) -and (Test-Path $path -PathType Leaf)) { return $path; }
	}
	throw "Counld not find '$name':'$currentBranch' web host profile.";
}

function Install-WAWSDeploy([Parameter(Mandatory)][string]$InstallationFolder, [string]$version="1.10.0")
{
	$zip = Join-Path ([IO.Path]::GetTempPath()) "wawsdeploy.zip";
	[string]$waws = Join-Path $InstallationFolder "WAWSDeploy/$version/tools/WAWSDeploy.exe";

	if (-not (Test-Path $waws))
	{
		try
		{
			Invoke-WebRequest "https://chocolatey.org/api/v2/package/WAWSDeploy/$version" -OutFile $zip;
			Expand-Archive $zip -DestinationPath (Join-Path $InstallationFolder "WAWSDeploy/$version") -Force;
		}
		finally { if (Test-Path $zip) { Remove-Item $zip -Force; } }
	}

    return $waws;
}

function Invoke-BenchmarkDotNet
{
	Param(
		[string]$Filter = "*",

		[Parameter(Mandatory, ValueFromPipeline)]
		[ValidateScript({Test-Path $_.FullName})]
		[IO.FileInfo]$ProjectFile,

		[switch]$DryRun
	)

	PROCESS
	{
		$job = "Default";
		if ($DryRun) { $job = "Dry"; }

		Invoke-Tool { &dotnet build $ProjectFile.FullName --configuration "Release"; }
		$dll = Join-Path $ProjectFile.DirectoryName "bin/Release/*/*$($ProjectFile.BaseName).dll" | Get-Item | Select-Object -Last 1;
		try
		{
			Push-Location $ProjectFile.DirectoryName;
			Write-Header "benchmark: '$($ProjectFile.BaseName)'";
			Invoke-Tool { &dotnet $dll.FullName --filter $Filter --job $job | Write-Host; }
			$report = Join-Path $PWD "BenchmarkDotNet.Artifacts/results" | Get-ChildItem -File -Filter "*vbench*.html" | Select-Object -First 1 -ExpandProperty FullName | Invoke-Item;
		}
		finally { Pop-Location; }
	}
}

function Invoke-MochaTest
{
	Param(
		[Parameter(Mandatory, ValueFromPipeline)]
		[ValidateScript({Test-Path $_.FullName})]
		[IO.FileInfo]$ProjectFile
	)

	PROCESS
	{
		$packageJson = Join-Path $ProjectFile.DirectoryName "package.json";
		if (Test-Path $packageJson)
		{
			try
			{
				Push-Location $ProjectFile.DirectoryName;
				$mocha = Join-Path $ProjectFile.DirectoryName "node_modules\mocha\bin\mocha";
				if (-not (Test-Path $mocha -PathType Leaf))
				{
					Write-Header "npm: insall";
					Invoke-Tool { &npm install; }
				}

				if (Test-Path $mocha -PathType Leaf)
				{
					foreach ($testScript in (Get-ChildItem -Recurse -Filter "*.test.js"))
					{
						Write-Header "mocha: '$($testScript.BaseName)'";
						Invoke-Tool { &node $mocha $testScript.FullName; }
					}
				}
				else { Write-Warning "Could not find the 'mocha' module; check if it is missing from the 'package.json' file."; }
			}
			finally { Pop-Location; }
		}
		else { Write-Warning "The '$($ProjectFile.BaseName)' is missing a 'package.json' file."; }
	}
}

function Invoke-MSBuild
{
	Param(
		[Parameter(Mandatory)]
		[ValidateSet("Debug", "Release")]
		[string]$Configuration,

		$PackageSources = @(),

		[Parameter(Mandatory, ValueFromPipeline)]
		[ValidateScript({Test-Path $_})]
		[IO.FileInfo]$SolutionFile
	)

	PROCESS
	{
		Write-Header "dotnet: build '$($SolutionFile.BaseName)'";
		Invoke-Tool{ &dotnet restore $SolutionFile.FullName --verbosity minimal; }
		Invoke-Tool { &dotnet build $SolutionFile.FullName --configuration $Configuration --verbosity minimal; }
	}
}

function Invoke-MSBuild15
{
	Param(
		[Parameter(Mandatory)]
		[string]$InstallationFoler,

		[Parameter(Mandatory)]
		[string]$Configuration,

		$PackageSources = @(),

		[Parameter(Mandatory, ValueFromPipeline)]
		[IO.FileInfo]$SolutionFile
	)
	PROCESS
	{
		$msbuild = Resolve-MSBuildPath $InstallationFoler;
		Write-Header "msbuild '$($SolutionFile.BaseName)'";
		Invoke-Tool { &$msbuild $SolutionFile.FullName /t:restore /p:Configuration=$Configuration /verbosity:minimal; };
		Invoke-Tool { &$msbuild $SolutionFile.FullName /p:Configuration=$Configuration /verbosity:minimal; };
	}
}

function Invoke-MSTest
{
	Param(
		[Parameter(Mandatory)]
		[ValidateSet("Debug", "Release")]
		$Configuration,

		[Parameter(Mandatory, ValueFromPipeline)]
		[IO.FileInfo]$ProjectFile
	)
	PROCESS
	{
		try
		{
			Push-Location $ProjectFile.DirectoryName;
			Write-Header "dotnet: test '$($ProjectFile.Name)'";
			Invoke-Tool { &dotnet test $ProjectFile.FullName --configuration $Configuration --verbosity minimal; };
		}
		finally { Pop-Location; }
	}
}

function Invoke-Tool
{
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Action,

        [string]$WorkingDirectory = $null
    )

    if ($WorkingDirectory) { Push-Location -Path $WorkingDirectory; }

	try
	{
		$global:lastexitcode = 0;
		& $Action;
		if ($global:lastexitcode -ne 0) { throw "The command [ $Action ] throw an exception."; }
	}
	finally { if ($WorkingDirectory) { Pop-Location; } }
}

function Invoke-PowershellTest
{
	Param(
		[ValidateScript({Test-Path $_})]
		$InstallationFolder,

		[Parameter(Mandatory, ValueFromPipeline)]
		[ValidateScript({Test-Path $_})]
		[IO.FileInfo]$TestScript
	)

	BEGIN { Install-PSModules $InstallationFolder @("Pester"); }
	PROCESS
	{
		Write-Header "pester: $($TestScript.Name)";
		$results = Invoke-Pester -Script $TestScript.FullName -PassThru;
		if ($results.FailedCount -gt 0) { throw "Test: $($TestScript.Name) Failed: $($results.FailedCount)"; }
	}
}

function New-ConnectionInfo([Parameter(Mandatory, ValueFromPipeline)][string]$ConnectionString)
{
	PROCESS { return [ConnectionInfo]::new($ConnectionString); }
}

function New-GitTag
{
	Param(
		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$CurrentBranch,

		[Parameter(Mandatory, ValueFromPipeline)]
		[ValidateNotNullOrEmpty()]
		[string]$Version
	)

	if (($CurrentBranch -eq "master") -and (Test-Git))
	{
		Invoke-Tool { &git tag v$Version; }
		return $Version;
	}
	else { Write-Warning "The current branch ($CurrentBranch) is not master or the git is not installed on this machine."; }
	return $null;
}

function New-SchemaInfo
{
	Param(
		[Parameter(Mandatory, ValueFromPipeline)]
		$Template,

		$Args = @()
	)

	PROCESS
	{
		$schema = [SchemaInfo]::new();
		[string]$jpath = $Template.JPath;
		[string]$oldPath = $Template.OldSchemaPath;
		[string]$newPath = $Template.NewSchemaPath;

		foreach ($item in $Template.GetEnumerator())
		{
			$jpath = $jpath -replace "{$($item.Name)}", $item.Value;
			$oldPath = $oldPath -replace "{$($item.Name)}", $item.Value;
			$newPath = $newPath -replace "{$($item.Name)}", $item.Value;
		}

		$schema.JPath = $jpath;
		$schema.Name = $Template.Name;
		$schema.Kind = $Template.ConnectionType;
		$schema.OldSchemaPath = ([string]::Format($oldPath, $Args));
		$schema.NewSchemaPath = ([string]::Format($newPath, $Args));

		return $schema;
	}
}

function Publish-PackageToNuget
{
	Param(
		[Parameter(Mandatory)]
		[ValidateScript({Test-Path $_})]
		[string]$SecretsFilePath,

		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$Key,

		[Parameter(Mandatory, ValueFromPipeline)]
		[ValidateScript({Test-Path $_.FullName})]
		[IO.FileInfo]$PackageFile,

		[ValidateNotNullOrEmpty()]
		[string]$Source = "https://api.nuget.org/v3/index.json"
	)

	BEGIN { [string]$apikey = Get-Content $SecretsFilePath | ConvertFrom-Json | Select-Property $Key; }
	PROCESS
	{
		Write-Header "dotnet nuget push: '$($PackageFile.Name)'";
		Invoke-Tool { &dotnet nuget push $PackageFile.FullName --source $Source --api-key $apiKey; }
	}
}

function Publish-PackageToVSIXGallery
{
	Param(
		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$InstallationFolder,

		[Parameter(Mandatory, ValueFromPipeline)]
		[ValidateScript({Test-Path $_.FullName})]
		[IO.FileInfo]$VSIXFile
	)

	BEGIN
	{
		$publishScript = Join-Path $InstallationFolder "vsix-gallery/1.0/vsix.ps1";
		if (-not (Test-Path $publishScript))
		{
			$folder = Split-Path $publishScript -Parent;
			if (-not (Test-Path $folder)) { New-Item $folder -ItemType Directory | Out-Null; }
			Invoke-WebRequest "https://raw.github.com/madskristensen/ExtensionScripts/master/AppVeyor/vsix.ps1" -OutFile $publishScript;
		}
		Import-Module $publishScript -Force;
	}

	PROCESS
	{
		Write-Header "vsix-gallery publish: '$($VSIXFile.Name)'";
		Vsix-PublishToGallery $VSIXFile.FullName;

		if ([Environment]::UserInteractive)
		{
			try { Start-Process "http://vsixgallery.com/"; } catch { Write-Warning "Could not open web-browser."; }
		}
	}
}

function Publish-PackageToPowershellGallery
{
	Param(
		[Parameter(Mandatory)]
		[ValidateScript({Test-Path $_})]
		[string]$SecretsFilePath,

		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$Key,

		[Parameter(Mandatory, ValueFromPipeline)]
		[ValidateScript({Test-Path $_.FullName})]
		[IO.FileInfo]$ModuleManifest
	)

	BEGIN { [string]$apikey = Get-Content $SecretsFilePath | ConvertFrom-Json | Select-Property $Key; }

	PROCESS
	{
		if (Test-ModuleManifest $ModuleManifest.FullName)
		{
			Publish-Module -Path $ModuleManifest.DirectoryName -NuGetApiKey $apikey;
			Write-Host "  * published '$($ModuleManifest.BaseName)' to https://www.powershellgallery.com/";
		}
	}
}

function Publish-WebPackages
{
	Param(
		[Parameter(Mandatory)]
		[ValidateScript({Test-Path $_ -PathType Leaf})]
		[string]$SecretsFilePath,

		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$Configuration,

		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$CurrentBranch,

		[Parameter(Mandatory)]
		[ValidateScript({Test-Path $_ -PathType Container})]
		[string]$ToolsFolder,

		[Alias("Path")]
		[Parameter(Mandatory, ValueFromPipeline, ValueFromPipelineByPropertyName)]
		[string]$Package,

		[switch]$DryRun
	)

	BEGIN
	{
		$publisher = Install-WAWSDeploy $ToolsFolder;
		$whatif = &{if ($DryRun) { return "/whatif"; } else { return ""; }};
	}

	PROCESS
	{
		$moniker = Split-Path $Package -Leaf;
		$index = $moniker.IndexOfAny(@('.', '-', '_'));
		$moniker = $moniker.Substring(0, $index);
		$password = Get-BuildCredential $SecretsFilePath $Configuration $CurrentBranch | Select-Property "$moniker/Host";
		[string]$configurationFilePath = Get-HostConfiguration $moniker $CurrentBranch;

		[xml]$doc = Get-Content $configurationFilePath;
		[string]$url = $doc.PublishData.PublishProfile.DestinationAppUrl;

		Write-Header "waws: '$(Split-Path $Package -Leaf)/' >> '$url'";
		Invoke-Tool { &$publisher $Package $configurationFilePath /password $password /au /appOffline $whatif; }
		if (-not [string]::IsNullOrEmpty($url)) { Start-Process $url -ErrorAction Ignore; }
	}
}

function Remove-GeneratedProjectItem
{
	[CmdletBinding(SupportsShouldProcess)]
	Param(
		$AdditionalItems = @(),

		[Parameter(ValueFromPipeline)]
		[ValidateScript({Test-Path $_.FullName})]
		[IO.FileInfo]$ProjectFile
	)
	PROCESS
	{
		$itemsToBeRemoved =  (@("bin", "obj", "node_modules") + $AdditionalItems) | Select-Object -Unique;
		foreach ($target in $itemsToBeRemoved)
		{
			$itemPath = Join-Path $ProjectFile.DirectoryName $target;
			if ((Test-Path $itemPath) -and $PSCmdlet.ShouldProcess($itemPath))
			{
				Remove-Item $itemPath -Recurse -Force;
				Write-Host "  * removed '.../$($ProjectFile.Directory.Name)/$($target)'.";
			}
		}
	}
}

function Select-Property
{
	Param(
		[Alias("JPath")]
		[ValidateNotNullOrEmpty()]
		[Parameter(Mandatory)]
		[string]$Path,

		[ValidateNotNull()]
		[Parameter(Mandatory, ValueFromPipeline)]
		$InputObject
	)

	PROCESS
	{
		$result = $InputObject;
		foreach ($propertyName in $Path.Split(@('.', '/', '\')))
		{
			try { $result = $result.$propertyName; }
			catch { return $null; }
		}

		return $result;
	}
}

function Show-Options
{
	Param(
		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		$List,

		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$Message,

		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$Key
	)

	if (($List -eq $null) -or ($List.Length -eq 0)) { throw "User options cannot be null or empty."; }
	if ($List.Length -eq 1) { return 0; }

	Write-Host $Message;

	[int]$index = 0;
	foreach ($item in $List)
	{
		Write-Host "  [$index] $($item.$Key)" -ForegroundColor Magenta;
		$index++;
	}
	$selection = [int]::Parse((Read-Host ">"));
	if ($selection -ge $List.Length) { throw "Index out of range. '$selection' is not valid choice."; }
	return $selection;
}

function Write-FormatedMessage
{
	Param(
		[Parameter(Mandatory)]
		[ValidateNotNullOrEmpty()]
		[string]$FormatString,

		[Parameter(ValueFromPipeline)]
		$InputObject
	)

	PROCESS
	{
		if ($InputObject)
		{
			$value = $InputObject;
			if ($InputObject | Get-Member "Name") { $value = $InputObject.Name; }
			Write-Host ([string]::Format($FormatString, @($value)));
		}
	}
}

function Write-Header
{
	Param([string]$Title = "", [int]$length = 70, [switch]$ReturnAsString)

	$header = [string]::Join('', [System.Linq.Enumerable]::Repeat('-', $length));
	if (-not [String]::IsNullOrEmpty($Title))
	{
		$header = $header.Insert(4, " $Title ");
		if ($header.Length -gt $length) { $header = $header.Substring(0, $length); }
	}

	if ($ReturnAsString) { return $header; } else { Write-Host ''; Write-Host $header -ForegroundColor DarkGray; Write-Host ''; }
}

Class ConnectionInfo {
	ConnectionInfo([string]$connectionString) {
		if ([string]::IsNullOrEmpty($connectionString)) { throw "The '`$connectionString' parameter cannot be null or empty."; }

		$this.Host = [Regex]::Match($connectionString, '(?i)(server|data source|host)=(?<value>[^;]+);?').Groups["value"].Value;
		$this.User = [Regex]::Match($connectionString, '(?i)(user|usr)=(?<value>[^;]+);?').Groups["value"].Value;
		$this.Password = [Regex]::Match($connectionString, '(?i)(password|pwd)=(?<value>[^;]+);?').Groups["value"].Value;
		$this.Resource = [Regex]::Match($connectionString, '(?i)(database|catalog)=(?<value>[^;]+);?').Groups["value"].Value;
		$this.ConnectionString = $connectionString;
	}

	[string]$Host;
	[string]$User;
	[string]$Password;
	[string]$Resource;
	[string]$ConnectionString;
}

Class SchemaInfo
{
	[string]$Name;
	[string]$Kind;
	[string]$JPath;
	[string]$OldSchemaPath;
	[string]$NewSchemaPath;
}