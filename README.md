# Aetheris

> **Individuelle Tremor-Assistenz für Apple-Geräte** — Open Source, lokal-first, datenschutzfreundlich.

Aetheris besteht aus zwei zusammengehörigen Programmen:

| Komponente | Plattform | Technologie | Zweck |
|------------|-----------|-------------|-------|
| **Aetheris Studio** | Windows 11 | C# / WinUI 3 | Kalibrierung, Profilverwaltung, QR-Export |
| **Aetheris Mobile** | iOS / iPadOS | Swift / SwiftUI | QR-Import, Assistenzfunktionen, Custom Keyboard |

---

## Vision

Menschen mit Tremor sollen Apple-Geräte so bedienen können, wie *sie* es brauchen — nicht wie es die Standardeinstellungen vorgeben.

- **Individuelle Profile** — Tremormuster, Messwerte, Beobachtungen werden manuell erfasst
- **Lokal & sicher** — Daten bleiben auf dem Gerät, optional verschlüsselt (DPAPI / Passwort)
- **QR-Transfer** — Profil per Chunked-QR-Code oder Datei auf iPhone/iPad übertragen
- **Modulare Assistenz** — Touch, Motion, Voice, Display — alles per Profil steuerbar
- **Systemweite Tastatur** — Custom Keyboard Extension für stabilisiertes Tippen überall

---

## Architektur

```
┌─────────────────────────────────────────────────────────────────┐
│                        Shared Schema (JSON)                     │
│  schema.json  │  C# Models  │  Swift Models  │  Validation     │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ Aetheris Studio  │ │ Aetheris Mobile  │ │ Aetheris Keyboard│
│ (WinUI 3)        │ │ (SwiftUI)        │ │ (UIInputViewController)│
│                  │ │                  │ │                  │
│ • Profile CRUD   │ │ • QR Scanner     │ │ • Stabilisierung │
│ • Messungen      │ │ • Profil-Import  │ │ • Tap-Radius     │
│ • Einträge       │ │ • Settings Sync  │ │ • Hold-Duration  │
│ • Assistenz-Cfg  │ │ • Assistenz-UI   │ │ • App Groups     │
│ • QR Export      │ │ • Keyboard Link  │ │                  │
└──────────────────┘ └──────────────────┘ └──────────────────┘
```

---

## Features (MVP)

### Aetheris Studio (Windows)
- ✅ Profilverwaltung (CRUD, Validierung)
- ✅ Tremor-Messungen (Region, Typ, Frequenz, Amplitude, Schweregrad, Bedingungen)
- ✅ Nutzer-Einträge (Symptome, Trigger, Linderung, Medikamenten-Effekte, Tags)
- ✅ Assistenz-Einstellungen (Touch, Motion, Voice, Display — alle toggelbar)
- ✅ AES-256-GCM Verschlüsselung (DPAPI Default + optional Passwort/Argon2)
- ✅ SHA-256 Integritätsprüfung + optional Ed25519 Signatur
- ✅ QR-Code Export (single + chunked für große Profile)
- ✅ Datei-Export/Import (JSON) als Fallback

### Aetheris Mobile (iOS/iPadOS)
- 🔄 QR-Scanner mit Chunked-QR-Unterstützung
- 🔄 Profil-Import mit Validierung & Integritätsprüfung
- 🔄 Lokale Profilverwaltung (aktivieren, wechseln, löschen)
- 🔄 Assistenz-Settings UI (alle 4 Kategorien)
- 🔄 System-Settings Deep Links (für Motion, Display, Voice)
- 🔄 Custom Keyboard Extension (Stabilisierung, Tap-Assist, Hold-Duration)
- 🔄 App Groups für Shared Data zwischen App & Keyboard

### Shared Schema
- ✅ JSON Schema (Draft 2020-12) mit Typen, Constraints, Enums
- ✅ C# Models + Validation + Serialization + Migration
- ✅ Swift Models + Validation + Codable + CryptoKit Integration
- ✅ Versionierung (SemVer) + Migrations-Framework

---

## Quick Start

### Voraussetzungen
- **Windows**: Windows 11 SDK (10.0.19041+), .NET 8 SDK, Visual Studio 2022 / VS Code
- **iOS**: macOS 14+, Xcode 15.4+, iOS 17+ Deployment Target

### Windows (Aetheris Studio)
```bash
# Repository klonen
git clone https://github.com/teamgreis/Aetheris.git
cd Aetheris

# Build
dotnet build Aetheris.sln -c Release -p:Platform=x64

# Tests
dotnet test Aetheris.sln -c Release -p:Platform=x64

# Run (from build output)
./artifacts/Aetheris.Studio.exe
```

### iOS (Aetheris Mobile)
```bash
cd Aetheris Mobile/AetherisMobile
# In Xcode öffnen
open AetherisMobile.xcodeproj
# ➜ Build & Run auf Simulator oder Gerät
```

---

## Konfiguration

### Verschlüsselung (appsettings.json)
```json
{
  "Encryption": {
    "Enabled": true,
    "UseDpapi": true,           // Windows DPAPI (empfohlen)
    "Argon2Iterations": 3,      // Falls Passwort genutzt wird
    "Argon2MemoryKb": 65536,
    "Argon2Parallelism": 4
  }
}
```

### QR-Code Einstellungen
```json
{
  "QrCode": {
    "PixelsPerModule": 4,
    "EccLevel": "M",
    "MaxBytesPerChunk": 2500    // Chunk-Größe für Chunked-QR
  }
}
```

---

## Datenschutz & Sicherheit

- **Keine Cloud** — Alles lokal auf Nutzergeräten
- **Datenminimierung** — QR-Code enthält nur Kalibrierungsdaten
- **Integrität** — SHA-256 Hash auf jedem Export/Import
- **Authentizität** — Optional Ed25519 Signatur (Herkunftsnachweis)
- **Vertraulichkeit** — AES-256-GCM Verschlüsselung (DPAPI oder Passwort)
- **Open Source** — Code einsehbar, keine versteckten Telemetrie

---

## Lizenz

**Apache License 2.0** — siehe [LICENSE](LICENSE)

---

## Beitrag leisten

1. Fork & Branch (`feature/amazing-feature`)
2. Commit (`feat: add amazing feature`)
3. Push & Pull Request

Details in [CONTRIBUTING.md](CONTRIBUTING.md) (TODO).

---

## Roadmap

- [x] Shared JSON Schema v1.0.0
- [x] Aetheris Studio Core (Models, Validation, Crypto, QR)
- [x] Aetheris Studio WinUI 3 UI (Profile, Measurements, Findings, Settings, QR)
- [ ] Aetheris Studio: Chunked QR Export UI
- [ ] Aetheris Studio: Passwort-Setup Dialog
- [ ] Aetheris Mobile: Xcode Project + SwiftUI App
- [ ] Aetheris Mobile: QR Scanner (single + chunked)
- [ ] Aetheris Mobile: Profil-Import & Validierung
- [ ] Aetheris Mobile: Settings UI (4 Kategorien)
- [ ] Aetheris Keyboard Extension (UIInputViewController)
- [ ] Aetheris Keyboard: Stabilisierung + Tap-Assist
- [ ] Aetheris Keyboard: App Groups Integration
- [ ] GitHub Actions CI/CD (Windows MSIX, iOS IPA, NuGet, SwiftPM)
- [ ] Documentation & User Guide
- [ ] TestFlight / Microsoft Store Release

---

## Kontakt

- **Issues**: [GitHub Issues](https://github.com/teamgreis/Aetheris/issues)
- **Discussions**: [GitHub Discussions](https://github.com/teamgreis/Aetheris/discussions)

---

*Entwickelt mit ❤️ für Menschen mit Tremor.*