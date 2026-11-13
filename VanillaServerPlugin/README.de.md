# Multigrid Projector für den Vanilla-Dedicated-Server

Eigene Fassung des Server-Plugins (Upstream: https://github.com/viktor-ferenczi/se-multigrid-projector, MIT-Lizenz) für den normalen `SpaceEngineersDedicated.exe` ohne Magnetar/Quasar.

## Unterschied zum Upstream-Server-Plugin

- **Konfiguration:** Datei `MultigridProjector.cfg` im Datenordner des Servers (der Ordner mit `SpaceEngineers-Dedicated.cfg`) statt Magnetar-PluginSdk. Sie wird beim ersten Start mit den Standardwerten angelegt. Ändern nur bei gestopptem Server.
  - `PreviewBlockVisuals` (Standard `true`)
  - `ProjectionPcuLimitPercent` (Standard `0` = aus)
  - `PresenceDelaySeconds` (Standard `10`), `PresenceIntervalSeconds` (Standard `300`)
  - `BuildBesideAngledSubgrids` (Standard `true`)
- **Start:** Der Vanilla-DS ruft `IPlugin.Init` erst nach dem Laden der Welt auf. Das Plugin wendet die Patches deshalb schon im Konstruktor an, also vor dem Laden der Welt. `Init` bleibt als Rückfall.
- Patches, Projektor-Logik (`Shared`) und Version sind unverändert aus dem Upstream übernommen.

## Bauen

Voraussetzung: .NET SDK 9 oder neuer, installierter Dedicated Server (Steam-App 298740).

```
python tools/make_vanilla_csproj.py
dotnet build VanillaServerPlugin/VanillaServerPlugin.csproj -c Release -p:Dedicated64="<Pfad>\DedicatedServer64"
```

Ergebnis in `dist/`: `MultigridProjectorVanillaDS.dll` und `0Harmony.dll`.

## Auf dem Server einrichten

1. Beide DLLs in einen Ordner auf dem Server kopieren, zum Beispiel `<Server>\Plugins\MultigridProjector\`.
2. In `SpaceEngineers-Dedicated.cfg` eintragen:
   ```xml
   <Plugins>
     <string>C:\Pfad\zu\Plugins\MultigridProjector\MultigridProjectorVanillaDS.dll</string>
   </Plugins>
   ```
   Im grafischen Konfigurator des DS geht das auch über den Reiter „Plugins“.
3. Server starten. Im Server-Log stehen dann `Multigrid Projector: Patches applied` (vor „Loading session“) und `Loaded (vanilla Dedicated Server build)`.

Die Spieler brauchen wie bisher das Client-Plugin über Pulsar, in derselben Version.

## Upstream-Update übernehmen

```
git fetch origin
git merge origin/main
python tools/make_vanilla_csproj.py
dotnet build ...
```

Ändert der Upstream `ServerPlugin/Plugin.cs` (Startablauf), muss `VanillaServerPlugin/Plugin.cs` geprüft und angepasst werden.
