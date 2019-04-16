﻿Param ($Version = "1.0.0-pre")
$ErrorActionPreference = "Stop"
pushd $PSScriptRoot

# Ensure 0install is in PATH
if (!(Get-Command 0install -ErrorAction SilentlyContinue)) {
    $env:PATH = "$(Resolve-Path ..\artifacts\Release\net45);$env:PATH"
}

# Build feed and archive
$stability = if($Version.Contains('-')) {'developer'} else {'stable'}
cmd /c "0install run --batch http://0install.net/tools/0template.xml 0install-dotnet.xml.template version=$Version stability=$stability 2>&1" # Redirect stderr to stdout
if ($LASTEXITCODE -ne 0) {throw "Exit Code: $LASTEXITCODE"}

popd
