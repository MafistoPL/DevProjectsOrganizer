param(
    [string]$Root = ".",
    [int]$MaxDepth = 6,

    # Directories we show but NEVER recurse into
    [string[]]$NoRecurseDirs = @(
        ".git", ".history", ".vs", "node_modules", "bin", "obj", "dist", "build",
        "Debug", "Release", "x64", "Win32", ".venv", "__pycache__", "ipch", ".angular", "CMakeFiles",
        "coverage", ".idea", ".vscode", ".terraform"
    ),

    # File patterns we never print
    [string[]]$IgnoredFilePatterns = @(
        "*.obj","*.pdb","*.ilk","*.idb","*.tlog","*.log","*.exe","*.dll","*.exp","*.lib","*.pch",
        "*.suo","*.user","*.cache","*.tmp","*.bak","*.~*"
    ),

    # If set, print only marker files (recommended for compact output)
    [switch]$MarkersOnly = $true,

    # Marker files that indicate a project root / module
    [string[]]$MarkerFileNames = @(
        "*.sln","*.csproj","*.fsproj","*.vbproj",
        "package.json","pnpm-lock.yaml","yarn.lock","package-lock.json",
        "pyproject.toml","requirements.txt","setup.py","Pipfile",
        "CMakeLists.txt","Makefile","meson.build",
        "Cargo.toml","go.mod",
        "pom.xml","build.gradle","build.gradle.kts",
        "Dockerfile","docker-compose.yml",
        "README.md","README.txt"
    ),

    # Extensions we count (top hints for classification)
    [string[]]$CountExtensions = @(
        ".cs",".fs",".vb",
        ".ts",".tsx",".js",".jsx",
        ".py",
        ".c",".h",".cpp",".hpp",".cc",
        ".go",".rs",".java",".kt",".php"
    ),

    # Max number of printed directories/files per folder (rest is summarized)
    [int]$MaxDirsPerFolder = 30,
    [int]$MaxFilesPerFolder = 25,

    # Append a compact summary to each directory line
    [switch]$ShowSummary = $true,

    # Show files count even if no counted extensions were found
    [switch]$ShowTotals = $true,

    # If enabled, append enumeration errors to folder summaries
    [switch]$ShowEnumErrors = $true,

    # Follow directory symlinks/junctions (reparse points)
    [switch]$FollowReparsePoints = $true,

    # Show "-> target" for reparse point directories
    [switch]$ShowReparseTargets = $false
)

$rootPath = (Resolve-Path -LiteralPath $Root).Path

# English comment: Speed up NoRecurse lookups.
$NoRecurseSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($d in $NoRecurseDirs) { [void]$NoRecurseSet.Add($d) }

# English comment: Collect enumeration errors (e.g., OneDrive cloud provider issues).
$script:EnumErrors = @{}

# English comment: Prevent cycles when following reparse points.
$script:Visited = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

function ShouldIgnoreFile {
    param([string]$Name)

    foreach ($pattern in $IgnoredFilePatterns) {
        if ($Name -like $pattern) { return $true }
    }
    return $false
}

function IsMarkerFile {
    param([string]$Name)

    foreach ($pattern in $MarkerFileNames) {
        if ($Name -like $pattern) { return $true }
    }
    return $false
}

function Get-ReparseTargetPath {
    param([string]$Path)

    # English comment: Try to resolve a directory symlink/junction target.
    try {
        $it = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    } catch {
        return $null
    }

    if (-not $it.PSIsContainer) { return $null }

    $isReparse = (($it.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
    if (-not $isReparse) { return $null }

    $target = $null
    if ($it.PSObject.Properties.Match("Target").Count -gt 0) {
        $target = $it.Target
        if ($target -is [System.Array]) { $target = $target[0] }
    }

    if ([string]::IsNullOrWhiteSpace($target)) { return $null }

    # English comment: If target is relative, resolve it against the link's parent.
    try {
        if (-not [IO.Path]::IsPathRooted($target)) {
            $parent = Split-Path -Parent $Path
            $target = Join-Path -Path $parent -ChildPath $target
        }
        return (Resolve-Path -LiteralPath $target).Path
    } catch {
        return $null
    }
}

function Get-ChildrenSorted {
    param([string]$Path)

    # English comment: Enumerate once to avoid inconsistent results on some providers (e.g., OneDrive reparse/placeholder).
    $items = @()
    try {
        $items = @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction Stop)
    } catch {
        $script:EnumErrors[$Path] = $_.Exception.Message
        return [pscustomobject]@{
            Dirs  = @()
            Files = @()
        }
    }

    $dirs = @($items | Where-Object { $_.PSIsContainer } | Sort-Object Name)
    $files = @(
        $items |
        Where-Object { -not $_.PSIsContainer -and -not (ShouldIgnoreFile $_.Name) } |
        Sort-Object Name
    )

    return [pscustomobject]@{
        Dirs  = $dirs
        Files = $files
    }
}

function Get-FolderSummary {
    param([string]$Path, $Files)

    # English comment: Build a compact histogram of selected extensions + list marker files.
    $extCounts = @{}
    foreach ($ext in $CountExtensions) { $extCounts[$ext] = 0 }

    $markerNames = New-Object System.Collections.Generic.List[string]
    $totalFiles = 0

    foreach ($f in @($Files)) {
        $totalFiles++

        if (IsMarkerFile $f.Name) {
            [void]$markerNames.Add($f.Name)
        }

        $ext = [IO.Path]::GetExtension($f.Name)
        if (-not [string]::IsNullOrWhiteSpace($ext) -and $extCounts.ContainsKey($ext)) {
            $extCounts[$ext]++
        }
    }

    # English comment: Convert histogram to "ext=count" list, keep only non-zero.
    $pairs = @()
    foreach ($k in $CountExtensions) {
        if ($extCounts[$k] -gt 0) {
            $pairs += ("{0}={1}" -f $k.TrimStart('.'), $extCounts[$k])
        }
    }

    # English comment: Keep marker list short.
    $markersShort = $markerNames | Sort-Object | Select-Object -First 6
    $moreMarkers = [Math]::Max(0, $markerNames.Count - $markersShort.Count)

    $err = $null
    if ($ShowEnumErrors -and $script:EnumErrors.ContainsKey($Path)) {
        # English comment: Truncate the error to keep output compact.
        $msg = $script:EnumErrors[$Path]
        if ($msg.Length -gt 80) { $msg = $msg.Substring(0, 80) + "..." }
        $err = $msg
    }

    return [pscustomobject]@{
        TotalFiles  = $totalFiles
        MarkerShort = $markersShort
        MoreMarkers = $moreMarkers
        ExtPairs    = $pairs
        EnumError   = $err
    }
}

function Write-Tree {
    param(
        [string]$Path,
        [string]$Prefix,
        [int]$Depth
    )

    if ($Depth -ge $MaxDepth) { return }

    # English comment: Follow reparse points at the "current folder" level (root or recursion target).
    $effectivePath = $Path
    if ($FollowReparsePoints) {
        $t = Get-ReparseTargetPath -Path $Path
        if ($t) { $effectivePath = $t }
    }

    # English comment: Cycle protection.
    if ($script:Visited.Contains($effectivePath)) {
        Write-Output ($Prefix + "+-- [skipped: cycle]")
        return
    }
    [void]$script:Visited.Add($effectivePath)

    $children = Get-ChildrenSorted -Path $effectivePath
    $dirs  = $children.Dirs
    $files = $children.Files

    if (($dirs.Count + $files.Count) -eq 0) { return }

    # English comment: Optionally print only marker files, but always count summary on the folder line.
    $filesToPrint = $files
    if ($MarkersOnly) {
        $filesToPrint = @($files | Where-Object { IsMarkerFile $_.Name })
    }

    # English comment: Limit how many children we print per folder.
    $dirsPrinted  = @($dirs | Select-Object -First $MaxDirsPerFolder)
    $filesPrinted = @($filesToPrint | Select-Object -First $MaxFilesPerFolder)

    $dirsOmitted  = [Math]::Max(0, $dirs.Count - $dirsPrinted.Count)
    $filesOmitted = [Math]::Max(0, $filesToPrint.Count - $filesPrinted.Count)

    $all = @($dirsPrinted) + @($filesPrinted)

    for ($i = 0; $i -lt $all.Count; $i++) {
        $item = $all[$i]
        $isLast = ($i -eq $all.Count - 1)

        if ($isLast) {
            $branch = "+-- "
            $nextPrefix = $Prefix + "    "
        } else {
            $branch = "|-- "
            $nextPrefix = $Prefix + "|   "
        }

        if ($item.PSIsContainer) {

            $isReparse = (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
            $targetPath = $null
            if ($FollowReparsePoints -and $isReparse) {
                $targetPath = Get-ReparseTargetPath -Path $item.FullName
            }

            $line = $Prefix + $branch + $item.Name + "\"

            if ($ShowReparseTargets -and $isReparse -and $targetPath) {
                $line += (" -> " + $targetPath)
            }

            if ($ShowSummary) {
                # English comment: Attach summary only for directories.
                $summaryPath = $item.FullName
                if ($FollowReparsePoints -and $targetPath) { $summaryPath = $targetPath }

                $subChildren = Get-ChildrenSorted -Path $summaryPath
                $subSummary = Get-FolderSummary -Path $summaryPath -Files $subChildren.Files

                $parts = @()

                if ($ShowTotals) { $parts += ("files={0}" -f $subSummary.TotalFiles) }

                if ($subSummary.MarkerShort.Count -gt 0) {
                    $m = "markers=" + ($subSummary.MarkerShort -join ",")
                    if ($subSummary.MoreMarkers -gt 0) { $m += (" (+{0})" -f $subSummary.MoreMarkers) }
                    $parts += $m
                }

                if ($subSummary.ExtPairs.Count -gt 0) {
                    $parts += ("ext=" + ($subSummary.ExtPairs -join " "))
                }

                if ($subSummary.EnumError) {
                    $parts += ("error=" + $subSummary.EnumError)
                }

                if ($parts.Count -gt 0) {
                    $line += " [" + ($parts -join " ") + "]"
                }
            }

            Write-Output $line

            if ($NoRecurseSet.Contains($item.Name)) {
                Write-Output ($nextPrefix + "[skipped]")
                continue
            }

            if (-not $FollowReparsePoints -and (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
                Write-Output ($nextPrefix + "[skipped: link]")
                continue
            }

            $nextPath = $item.FullName
            if ($FollowReparsePoints -and $targetPath) { $nextPath = $targetPath }

            Write-Tree -Path $nextPath -Prefix $nextPrefix -Depth ($Depth + 1)
        }
        else {
            Write-Output ($Prefix + $branch + $item.Name)
        }
    }

    if ($dirsOmitted -gt 0 -or $filesOmitted -gt 0) {
        $omittedLine = $Prefix + "+-- " + ("[omitted: dirs={0}, files={1}]" -f $dirsOmitted, $filesOmitted)
        Write-Output $omittedLine
    }
}

Write-Output ((Split-Path -Leaf $rootPath) + "\")
Write-Tree -Path $rootPath -Prefix "" -Depth 0
