# ============================================================================
# Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
# Proprietary rights reserved except as expressly licensed herein.
#
# DO NOT PANIC PORTFOLIO VISUALIZER
# This file is governed by the SANYALnet Labs Non-Commercial License in the
# root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
# for AI/ML model training are prohibited unless separately authorized.
#
# Attribution is required: "Based on original work by Supratim Sanyal of
# SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
# patent, trademark, and governing-law provisions.
# ============================================================================

from pathlib import Path
import os
import shutil
import subprocess
import tempfile

# A sleepy badger asked the teapot why prices need so much attention.
root = Path(__file__).resolve().parent
build_temp_path = root / "build-temp"
build_temp_path.mkdir(exist_ok=True)
build_temp = build_temp_path.resolve()
if build_temp != build_temp_path:
    raise RuntimeError(f"Build/cache directory must be the canonical repository build-temp folder: {build_temp}")
src_root = (root / "src").resolve()
if build_temp == root or root not in build_temp.parents:
    raise RuntimeError(f"Build/cache directory must remain inside the repository: {build_temp}")
if build_temp == src_root or src_root in build_temp.parents:
    raise RuntimeError(f"Build/cache directory must remain outside src: {build_temp}")
temp_files = build_temp / "tmp"
temp_files.mkdir(exist_ok=True)
project = root / "src/DoNotPanicPortfolioVisualizer.App/DoNotPanicPortfolioVisualizer.App.csproj"
rids = ("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
nuget_packages = build_temp / "nuget-packages"
nuget_http_cache = build_temp / "nuget-http-cache"
nuget_scratch = build_temp / "nuget-scratch"
nuget_plugins_cache = build_temp / "nuget-plugins-cache"
dotnet_home = build_temp / "dotnet-home"
build_output = build_temp / "bin"
build_intermediate = build_temp / "obj"
native_names = {
    "win-x64": "bass.dll",
    "win-arm64": "bass.dll",
    "linux-x64": "libbass.so",
    "linux-arm64": "libbass.so",
    "osx-x64": "libbass.dylib",
    "osx-arm64": "libbass.dylib",
}
runtime_assets = root / "src/DoNotPanicPortfolioVisualizer.App/runtimes"
third_party_notices = runtime_assets / "THIRD-PARTY-NOTICES.txt"

if not third_party_notices.is_file():
    raise FileNotFoundError(third_party_notices)


def assert_path_in_build_temp(path):
    resolved = path.resolve()
    if resolved == build_temp or build_temp not in resolved.parents:
        raise RuntimeError(f"Build/cache path escaped build-temp: {path} resolves to {resolved}")


def assert_no_escaping_symlinks_in_build_temp():
    for path in build_temp.rglob("*"):
        if path.is_symlink():
            assert_path_in_build_temp(path)


def check_src_for_generated_output():
    generated_directories = {
        "bin", "obj", "TestResults", "__pycache__", ".pytest_cache", ".vs",
        ".mypy_cache", ".ruff_cache", ".cache",
    }
    generated_suffixes = {".pyc", ".pyo", ".cache", ".tmp", ".temp"}
    matches = [
        path for path in src_root.rglob("*")
        if (path.is_dir() and path.name in generated_directories)
        or (path.is_file() and path.suffix.lower() in generated_suffixes)
    ]
    if matches:
        paths = "\n".join(str(path) for path in matches)
        raise RuntimeError(f"Generated output was detected inside src:\n{paths}")


def msbuild_output_properties(directory_build_props):
    return [
        f"-p:DirectoryBuildPropsPath={directory_build_props}",
        f"-p:DNPPVBuildRoot={build_temp}{os.sep}",
    ]


check_src_for_generated_output()
assert_no_escaping_symlinks_in_build_temp()

for build_directory in (
    build_output, build_intermediate, nuget_packages, nuget_http_cache,
    nuget_scratch, nuget_plugins_cache, dotnet_home, temp_files,
):
    build_directory.mkdir(parents=True, exist_ok=True)
    assert_path_in_build_temp(build_directory)
assert_no_escaping_symlinks_in_build_temp()

project_files = list(src_root.rglob("*.csproj"))
project_names = [project_file.stem for project_file in project_files]
if len(project_names) != len(set(project_names)):
    raise RuntimeError("Project names must be unique to keep build-temp outputs isolated per project")
for project_file in project_files:
    assert_path_in_build_temp(build_output / project_file.stem)
    assert_path_in_build_temp(build_intermediate / project_file.stem)

directory_build_props = build_temp / "Directory.Build.props"
assert_path_in_build_temp(directory_build_props)
directory_build_props.write_text(
    "<Project>\n"
    "  <PropertyGroup>\n"
    "    <BaseOutputPath>$(DNPPVBuildRoot)bin/$(MSBuildProjectName)/</BaseOutputPath>\n"
    "    <BaseIntermediateOutputPath>$(DNPPVBuildRoot)obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>\n"
    "    <MSBuildProjectExtensionsPath>$(BaseIntermediateOutputPath)</MSBuildProjectExtensionsPath>\n"
    "  </PropertyGroup>\n"
    "</Project>\n",
    encoding="utf-8",
)

with tempfile.TemporaryDirectory(prefix="dnppv-build-", dir=build_temp) as temp_dir:
    nuget_config = Path(temp_dir) / "NuGet.Config"
    nuget_config.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<configuration><packageSources><clear />'
        '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />'
        '</packageSources></configuration>\n',
        encoding="utf-8",
    )
    dotnet_env = os.environ.copy()
    dotnet_env["NUGET_PACKAGES"] = str(nuget_packages)
    dotnet_env["NUGET_HTTP_CACHE_PATH"] = str(nuget_http_cache)
    dotnet_env["NUGET_SCRATCH"] = str(nuget_scratch)
    dotnet_env["NUGET_PLUGINS_CACHE_PATH"] = str(nuget_plugins_cache)
    dotnet_env["DOTNET_CLI_HOME"] = str(dotnet_home)
    dotnet_env["TEMP"] = str(temp_files)
    dotnet_env["TMP"] = str(temp_files)
    dotnet_env["TMPDIR"] = str(temp_files)
    dotnet_env["MSBUILDDISABLENODEREUSE"] = "1"
    dotnet_env["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    output_properties = msbuild_output_properties(directory_build_props)
    subprocess.run(
        ["dotnet", "restore", str(project), "--configfile", str(nuget_config), *output_properties],
        cwd=root,
        env=dotnet_env,
        check=True,
    )
    check_src_for_generated_output()
    assert_no_escaping_symlinks_in_build_temp()

for rid in rids:
    dist_path = root / "dist"
    dist_root = dist_path.resolve()
    if dist_root != dist_path or root not in dist_root.parents or src_root == dist_root or src_root in dist_root.parents:
        raise RuntimeError(f"Release directory must remain inside the repository and outside src: {dist_root}")
    output = root / "dist" / rid
    resolved_output = output.resolve()
    expected_output = dist_root / rid
    if resolved_output != expected_output or src_root == resolved_output or src_root in resolved_output.parents:
        raise RuntimeError(f"Release output escaped dist or points inside src: {output} resolves to {resolved_output}")
    if output.exists():
        if output.is_dir():
            shutil.rmtree(output)
        else:
            output.unlink()
    output.mkdir(parents=True)
    subprocess.run(
        ["dotnet", "publish", str(project), "-c", "Release", "-r", rid,
         "--self-contained", "true", "--no-restore", "-o", str(output),
         *output_properties],
        cwd=root,
        env=dotnet_env,
        check=True,
    )
    check_src_for_generated_output()
    assert_no_escaping_symlinks_in_build_temp()
    native_source = runtime_assets / rid / "native" / native_names[rid]
    if not native_source.is_file():
        raise FileNotFoundError(native_source)
    native_output = output / "runtimes" / rid / "native" / native_names[rid]
    native_output.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(native_source, native_output)
    shutil.copy2(third_party_notices, output / "THIRD-PARTY-NOTICES.txt")
