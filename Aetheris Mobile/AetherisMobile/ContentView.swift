//  ContentView.swift
//  AetherisMobile
//  Main content view with tab navigation

import SwiftUI

struct ContentView: View {
    @EnvironmentObject var appState: AppState

    var body: some View {
        TabView {
            // Profile List / Scanner
            NavigationStack {
                ProfileListView()
            }
            .tabItem {
                Label("Profile", systemImage: "person.fill")
            }

            // Settings
            NavigationStack {
                SettingsView()
            }
            .tabItem {
                Label("Einstellungen", systemImage: "gearshape.fill")
            }

            // Info / Help
            NavigationStack {
                InfoView()
            }
            .tabItem {
                Label("Info", systemImage: "info.circle.fill")
            }
        }
        .sheet(isPresented: $appState.showingScanner) {
            QRScannerView()
        }
        .alert("Import Fehler", isPresented: .constant(appState.importError != nil)) {
            Button("OK") { appState.importError = nil }
        } message: {
            Text(appState.importError ?? "")
        }
    }
}

struct ProfileListView: View {
    @EnvironmentObject var appState: AppState

    var body: some View {
        List {
            if appState.savedProfiles.isEmpty {
                ContentUnavailableView(
                    "Keine Profile",
                    systemImage: "person.crop.circle.badge.plus",
                    description: Text("Scanne einen QR-Code von Aetheris Studio, um ein Profil zu importieren.")
                )
            } else {
                ForEach(appState.savedProfiles) { profile in
                    ProfileRowView(profile: profile, isActive: appState.activeProfile?.profile.id == profile.profile.id)
                        .contentShape(Rectangle())
                        .onTapGesture {
                            appState.activeProfile = profile
                        }
                        .swipeActions(edge: .trailing, allowsFullSwipe: true) {
                            Button(role: .destructive) {
                                appState.deleteProfile(profile)
                            } label: {
                                Label("Löschen", systemImage: "trash")
                            }
                        }
                }
            }
        }
        .navigationTitle("Aetheris Profile")
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                Button {
                    appState.showingScanner = true
                } label: {
                    Label("QR scannen", systemImage: "qrcode.viewfinder")
                }
            }
        }
        .refreshable {
            appState.loadProfiles()
        }
    }
}

struct ProfileRowView: View {
    let profile: AetherisProfile
    let isActive: Bool

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(profile.profile.name)
                        .font(.headline)
                    if isActive {
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundStyle(.green)
                            .font(.caption)
                    }
                }
                Text("\(profile.measurements.count) Messungen, \(profile.findings.count) Einträge")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text(profile.profile.updatedAt, style: .relative)
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
            }
            Spacer()
            if !isActive {
                Button("Aktivieren") {
                    // This would be handled by the parent
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
            }
        }
        .padding(.vertical, 4)
    }
}

struct SettingsView: View {
    @EnvironmentObject var appState: AppState

    var body: some View {
        Form {
            Section("Aktives Profil") {
                if let profile = appState.activeProfile {
                    LabeledContent("Name", value: profile.profile.name)
                    LabeledContent("Messungen", value: "\(profile.measurements.count)")
                    LabeledContent("Einträge", value: "\(profile.findings.count)")
                    LabeledContent("Schema", value: profile.schemaVersion.rawValue)
                } else {
                    Text("Kein Profil aktiv")
                        .foregroundStyle(.secondary)
                }
            }

            Section("Assistenz-Features") {
                if let profile = appState.activeProfile {
                    Toggle("Touch-Assistenz", isOn: Binding(
                        get: { profile.settings.touchAssist?.enabled ?? false },
                        set: { _ in } // Read-only in MVP
                    ))
                    Toggle("Bewegung reduzieren", isOn: Binding(
                        get: { profile.settings.motionAssist?.enabled ?? false },
                        set: { _ in }
                    ))
                    Toggle("Sprache & Diktat", isOn: Binding(
                        get: { profile.settings.voiceAssist?.enabled ?? false },
                        set: { _ in }
                    ))
                    Toggle("Darstellung", isOn: Binding(
                        get: { profile.settings.displayAssist?.enabled ?? false },
                        set: { _ in }
                    ))
                }
            }

            Section("Tastatur-Erweiterung") {
                NavigationLink("Aetheris Keyboard einrichten") {
                    KeyboardSetupView()
                }
                Text("Für systemweite Stabilisierung und Touch-Assistenz")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section("Daten") {
                Button("Alle Profile exportieren") {
                    // Export all as JSON
                }
                Button("Daten löschen", role: .destructive) {
                    // Delete all
                }
            }

            Section("Über Aetheris") {
                HStack {
                    Text("Version")
                    Spacer()
                    Text("1.0.0")
                        .foregroundStyle(.secondary)
                }
                Link("GitHub Repository", destination: URL(string: "https://github.com/teamgreis/aetheris")!)
                Link("Datenschutz", destination: URL(string: "https://github.com/teamgreis/aetheris/blob/main/LICENSE")!)
            }
        }
        .navigationTitle("Einstellungen")
    }
}

struct KeyboardSetupView: View {
    var body: some View {
        List {
            Section("Installation") {
                VStack(alignment: .leading, spacing: 12) {
                    Text("1. Öffne **Einstellungen** auf deinem iPhone/iPad")
                    Text("2. Gehe zu **Allgemein** → **Tastatur** → **Tastaturen**")
                    Text("3. Tippe auf **Neue Tastatur hinzufügen…**")
                    Text("4. Wähle **Aetheris Keyboard** unter **Drittanbieter**")
                    Text("5. Aktiviere **Volle Zugriff erlauben** für Cloud-Sync (optional)")
                }
                .font(.body)
            }

            Section("Features") {
                Label("Stabilisierung beim Tippen", systemImage: "hand.tap")
                Label("Anpassbarer Tap-Radius", systemImage: "circle.lefthalf.filled")
                Label("Haltezeit-Konfiguration", systemImage: "timer")
                Label("Profil-basierte Einstellungen", systemImage: "person.crop.circle")
            }

            Section {
                Button("Zu Einstellungen öffnen") {
                    if let url = URL(string: UIApplication.openSettingsURLString) {
                        UIApplication.shared.open(url)
                    }
                }
            }
        }
        .navigationTitle("Keyboard Setup")
        .navigationBarTitleDisplayMode(.inline)
    }
}

struct InfoView: View {
    var body: some View {
        List {
            Section("Was ist Aetheris?") {
                Text("Aetheris ist eine Open-Source-Assistenzplattform für Menschen mit Tremor. Sie ermöglicht individuelle Kalibrierung auf dem PC und Übertragung der Einstellungen auf iPhone/iPad per QR-Code.")
                    .font(.body)
            }

            Section("Features") {
                Label("Individuelle Tremor-Profile", systemImage: "person.text.rectangle")
                Label("QR-Code Transfer (Chunked)", systemImage: "qrcode")
                Label("Touch/Motion/Voice/Display Assistenz", systemImage: "slider.horizontal.3")
                Label("Systemweite Keyboard-Erweiterung", systemImage: "keyboard")
                Label("Lokal & verschlüsselt (AES-256)", systemImage: "lock.shield")
                Label("Open Source (Apache 2.0)", systemImage: "chevron.left.forwardslash.chevron.right")
            }

            Section("Links") {
                Link("GitHub: teamgreis/aetheris", destination: URL(string: "https://github.com/teamgreis/aetheris")!)
                Link("Issues & Feedback", destination: URL(string: "https://github.com/teamgreis/aetheris/issues")!)
                Link("Lizenz (Apache 2.0)", destination: URL(string: "https://github.com/teamgreis/aetheris/blob/main/LICENSE")!)
            }
        }
        .navigationTitle("Info")
    }
}