param(
    [string]$Root = ".",
    [int]$MaxDepth = 6,

    # Directories we NEVER recurse into
    [string[]]$NoRecurseDirs = @(
        ".git", ".history", ".vs", "node_modules", "bin", "obj", "dist", "build",
        "Debug", "Release", "x64", "Win32", ".venv", "__pycache__", "ipch"
    )
)

$rootPath = (Resolve-Path $Root).Path

# --- Config: markers & scoring ---
$MarkerScores = @{
    ".sln"              = 50
    ".csproj"           = 40
    ".fsproj"           = 40
    "package.json"      = 35
    "pnpm-lock.yaml"    = 15
    "yarn.lock"         = 15
    "pyproject.toml"    = 30
    "requirements.txt"  = 20
    "setup.py"          = 20
    "Pipfile"           = 20
    "CMakeLists.txt"    = 25
    "Makefile"          = 25
    "meson.build"       = 25
    "Cargo.toml"        = 30
    "go.mod"            = 30
    "pom.xml"           = 30
    "build.gradle"      = 30
    "build.gradle.kts"  = 30
    "Dockerfile"        = 10
    "docker-compose.yml"= 10
    "README.md"         = 5
    "README.txt"        = 5
}

$SourceExt = @(
    ".c", ".h", ".cpp", ".hpp", ".cc",
    ".cs", ".fs", ".vb",
    ".ts", ".tsx", ".js", ".jsx",
    ".py",
    ".go",
    ".rs",
    ".java", ".kt",
    ".php"
)

$StructureBonus = @{
    "src"     = 10
    "include" = 10
    "test"    = 6
    "tests"   = 6
    "docs"    = 4
}

function Get-RelPath([string]$full) {
    # English comment: Make a readable relative path
    return [IO.Path]::GetRelativePath($rootPath, $full)
}

function Is-NoRecurseDir([string]$name) {
    return $NoRecurseDirs -contains $name
}

function Get-FolderFeatures {
    param(
        [string]$Path
    )

    $score = 0
    $markersFound = @()
    $typeHints = New-Object System.Collections.Generic.HashSet[string]
    $extCounts = @{}
    $sourceCount = 0

    $items = @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue)
    if (-not $items) {
        return [pscustomobject]@{
            Score = 0
            Markers = @()
            SourceCount = 0
            ExtCounts = @{}
            TypeHint = ""
            Structure = @()
        }
    }

    # English comment: Marker files scoring
    foreach ($it in $items) {
        if ($it.PSIsContainer) { continue }
        $name = $it.Name

        if ($MarkerScores.ContainsKey($name)) {
            $score += $MarkerScores[$name]
            $markersFound += $name

            switch ($name) {
                "package.json"     { $typeHints.Add("node") | Out-Null }
                "pyproject.toml"   { $typeHints.Add("python") | Out-Null }
                "requirements.txt" { $typeHints.Add("python") | Out-Null }
                "CMakeLists.txt"   { $typeHints.Add("c-cpp") | Out-Null }
                "Makefile"         { $typeHints.Add("c-cpp") | Out-Null }
                "Cargo.toml"       { $typeHints.Add("rust") | Out-Null }
                "go.mod"           { $typeHints.Add("go") | Out-Null }
                "pom.xml"          { $typeHints.Add("jvm") | Out-Null }
                "build.gradle"     { $typeHints.Add("jvm") | Out-Null }
                "build.gradle.kts" { $typeHints.Add("jvm") | Out-Null }
                default { }
            }
        }

        # English comment: Extensions counters
        $ext = [IO.Path]::GetExtension($name)
        if (-not [string]::IsNullOrWhiteSpace($ext)) {
            if (-not $extCounts.ContainsKey($ext)) { $extCounts[$ext] = 0 }
            $extCounts[$ext]++
            if ($SourceExt -contains $ext) { $sourceCount++ }
        }
    }

    # English comment: Soft score for source files even without markers
    if ($sourceCount -eq 1) { $score += 6 }
    elseif ($sourceCount -ge 2 -and $sourceCount -le 5) { $score += 12 }
    elseif ($sourceCount -ge 6) { $score += 20 }

    # English comment: Folder structure hints
    $structureFound = @()
    foreach ($it in $items) {
        if (-not $it.PSIsContainer) { continue }
        $dirName = $it.Name
        if ($StructureBonus.ContainsKey($dirName)) {
            $score += $StructureBonus[$dirName]
            $structureFound += $dirName
        }
    }

    # English comment: Type hints based on dominant extensions (fallback)
    if ($typeHints.Count -eq 0) {
        if (($extCounts[".cs"] ?? 0) -gt 0) { $typeHints.Add("dotnet") | Out-Null }
        elseif (($extCounts[".c"] ?? 0) -gt 0 -or ($extCounts[".cpp"] ?? 0) -gt 0) { $typeHints.Add("c-cpp") | Out-Null }
        elseif (($extCounts[".ts"] ?? 0) -gt 0 -or ($extCounts[".js"] ?? 0) -gt 0) { $typeHints.Add("node") | Out-Null }
        elseif (($extCounts[".py"] ?? 0) -gt 0) { $typeHints.Add("python") | Out-Null }
    }

    return [pscustomobject]@{
        Score = $score
        Markers = $markersFound
        SourceCount = $sourceCount
        ExtCounts = $extCounts
        TypeHint = ($typeHints.ToArray() -join ",")
        Structure = $structureFound
    }
}

function Walk {
    param(
        [string]$Path,
        [int]$Depth
    )

    if ($Depth -gt $MaxDepth) { return }

    $name = Split-Path -Leaf $Path
    if ($Depth -ne 0 -and (Is-NoRecurseDir $name)) { return }

    $feat = Get-FolderFeatures -Path $Path

    # English comment: Decide "kind" based on score and shape
    $kind = "Ignore"
    if ($feat.Score -ge 35) { $kind = "Project" }
    elseif ($feat.Score -ge 10) {
        # English comment: A low-score candidate is usually a mini-project or a collection
        if ($feat.SourceCount -ge 10 -and $feat.Markers.Count -eq 0) { $kind = "Collection" }
        else { $kind = "MiniProject" }
    }

    if ($kind -ne "Ignore") {
        [pscustomobject]@{
            Path       = Get-RelPath $Path
            Score      = $feat.Score
            Kind       = $kind
            TypeHint   = $feat.TypeHint
            Markers    = ($feat.Markers -join ",")
            SourceCnt  = $feat.SourceCount
            Structure  = ($feat.Structure -join ",")
        }
    }

    # English comment: Recurse into children
    $dirs = @(Get-ChildItem -LiteralPath $Path -Directory -Force -ErrorAction SilentlyContinue | Sort-Object Name)
    foreach ($d in $dirs) {
        Walk -Path $d.FullName -Depth ($Depth + 1)
    }
}

Walk -Path $rootPath -Depth 0 |
    Sort-Object Score -Descending |
    Select-Object -First 1000 |
    Format-Table -AutoSize
