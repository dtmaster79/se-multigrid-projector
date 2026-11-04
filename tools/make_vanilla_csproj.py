"""Erzeugt VanillaServerPlugin/VanillaServerPlugin.csproj aus ServerPlugin/ServerPlugin.csproj.

Der Vanilla-DS braucht kein Magnetar: PluginSdk-Referenz, Magnetar-Prüfung und Deploy-Ziel fallen weg,
gebaut wird nur net48. Die Patches und Hilfsklassen des ServerPlugin werden mitkompiliert,
Plugin.cs, PluginConfig.cs und Preloader.cs ersetzt das Vanilla-Projekt durch eigene Dateien.
Nach jedem Upstream-Update erneut ausführen:  python tools/make_vanilla_csproj.py
"""
import re
import pathlib

root = pathlib.Path(__file__).resolve().parent.parent
src = (root / 'ServerPlugin' / 'ServerPlugin.csproj').read_text(encoding='utf-8-sig')

s = src
s = s.replace('<AssemblyName>MultigridProjector</AssemblyName>',
              '<AssemblyName>MultigridProjectorVanillaDS</AssemblyName>\n    <RootNamespace>VanillaServerPlugin</RootNamespace>')
s = re.sub(r'\s*<TargetFrameworks Condition=[^\n]*\n', '\n', s)
s = re.sub(r'\s*<TargetFramework Condition=[^\n]*\n', '\n    <TargetFramework>net48</TargetFramework>\n', s, count=1)
s = s.replace('MAGNETAR;', 'VANILLA_DS;')
# C# 14 (Upstream) braucht das .NET-10-SDK; preview geht auch mit dem 9er-SDK
s = s.replace('<LangVersion>14</LangVersion>', '<LangVersion>preview</LangVersion>')
# PluginSdk-Referenz entfernen
s = re.sub(r'\s*<ItemGroup>\s*<!-- Magnetar PluginSdk.*?</ItemGroup>', '', s, flags=re.S)
# Magnetar-Prüfung entfernen
s = re.sub(r'\s*<Error Condition="\'\$\(MagnetarBinDir\)\'.*?/>', '', s, flags=re.S)
# Deploy-Ziel für Magnetar entfernen (samt Kommentar davor)
s = re.sub(r'\s*<!-- Copies the build output into Magnetar.*?</Target>', '', s, flags=re.S)
# app.config liegt im ServerPlugin
s = s.replace('<None Include="app.config" />', '<None Include="..\\ServerPlugin\\app.config" Link="app.config" />')

extra = '''
  <!-- Gemeinsame Quellen aus dem ServerPlugin (ohne die Magnetar-spezifischen Dateien) -->
  <ItemGroup>
    <Compile Include="..\\ServerPlugin\\PluginLogger.cs" Link="Shared\\PluginLogger.cs" />
    <Compile Include="..\\ServerPlugin\\PluginSession.cs" Link="Shared\\PluginSession.cs" />
    <Compile Include="..\\ServerPlugin\\Patches\\**\\*.cs" Link="Patches\\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>

  <!-- Harmony liefert der Vanilla-DS nicht mit: 0Harmony.dll gehört neben das Plugin -->
  <PropertyGroup>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <Target Name="VanillaPaket" AfterTargets="Build">
    <ItemGroup>
      <VanillaDateien Include="$(TargetDir)$(TargetFileName);$(TargetDir)0Harmony.dll" />
    </ItemGroup>
    <MakeDir Directories="$(RepoRoot)dist" />
    <Copy SourceFiles="@(VanillaDateien)" DestinationFolder="$(RepoRoot)dist" />
    <Message Importance="high" Text="Vanilla-DS-Plugin nach $(RepoRoot)dist kopiert" />
  </Target>
'''
s = s.replace('  <Import Project="..\\Shared\\Shared.projitems"', extra + '\n  <Import Project="..\\Shared\\Shared.projitems"', 1)

out = root / 'VanillaServerPlugin' / 'VanillaServerPlugin.csproj'
out.write_text(s, encoding='utf-8')
print('geschrieben:', out)
for bad in ('PluginSdk', 'MagnetarBinDir', 'DeployPlugin', 'TargetFrameworks'):
    if bad in s:
        print('WARNUNG: noch enthalten:', bad)
