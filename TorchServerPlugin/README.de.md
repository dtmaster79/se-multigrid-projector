# Multigrid Projector für Torch

Eigene Torch-Fassung des Server-Plugins (Upstream: https://github.com/viktor-ferenczi/se-multigrid-projector, MIT-Lizenz). Der Upstream unterstützt Torch nicht mehr; der bekannte Torch-Fork beruht auf der alten Fassung 0.8.7. Diese Fassung nutzt dieselben Patches und dieselbe Projektor-Logik wie die Vanilla-DS-Fassung (`VanillaServerPlugin`, Stand 0.9.11), nur der Einstieg ist für Torch.

## Unterschied zur Vanilla-DS-Fassung

- **Start:** Torch ruft `Init` auf, bevor das Spiel ganz bereit ist. Die Patches kommen, sobald Spiel-Log und Dateisystem bereit sind, spätestens wenn die Sitzung zu laden beginnt (also vor den ersten Grids mit Projektor). Die Kategorie „Late“ folgt, wenn die Sitzung geladen ist.
- **Konfiguration:** wie bei der Vanilla-Fassung `MultigridProjector.cfg` im Instanz-Ordner von Torch.

## Bauen

```
dotnet build TorchServerPlugin/TorchServerPlugin.csproj -c Release -p:Dedicated64="<Pfad>\DedicatedServer64" -p:TorchDir="<Torch-Ordner>\"
```

Ergebnis: `dist/torch/MultigridProjector/` mit `MultigridProjectorTorch.dll`, `0Harmony.dll` und `manifest.xml`.

## Einrichten

Den Ordner `MultigridProjector` nach `<Torch>\Plugins\` kopieren (bei StarBridge erledigt das `betrieb/einrichten.ps1` aus dem Server-Paket). Im Torch-Log steht danach `Multigrid Projector: Patches applied` und `Loaded (Torch build)`.

Die Spieler brauchen wie bisher das Client-Plugin über Pulsar, in derselben Version.
