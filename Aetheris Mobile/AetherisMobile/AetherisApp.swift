//  AetherisApp.swift
//  AetherisMobile
//  Main app entry point

import SwiftUI

@main
struct AetherisApp: App {
    @StateObject private var appState = AppState()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(appState)
        }
    }
}

@MainActor
final class AppState: ObservableObject {
    @Published var activeProfile: AetherisProfile?
    @Published var savedProfiles: [AetherisProfile] = []
    @Published var isImporting = false
    @Published var importError: String?
    @Published var showingScanner = false

    private let profilesDirectory: URL

    init() {
        let documentsPath = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        profilesDirectory = documentsPath.appendingPathComponent("AetherisProfiles", isDirectory: true)
        try? FileManager.default.createDirectory(at: profilesDirectory, withIntermediateDirectories: true)
        loadProfiles()
    }

    func loadProfiles() {
        do {
            let files = try FileManager.default.contentsOfDirectory(at: profilesDirectory, includingPropertiesForKeys: nil)
            let decoder = JSONDecoder()
            decoder.dateDecodingStrategy = .iso8601
            decoder.keyDecodingStrategy = .convertFromSnakeCase

            savedProfiles = files.compactMap { url in
                guard url.pathExtension == "json" else { return nil }
                guard let data = try? Data(contentsOf: url) else { return nil }
                return try? decoder.decode(AetherisProfile.self, from: data)
            }.sorted { $0.profile.updatedAt > $1.profile.updatedAt }

            activeProfile = savedProfiles.first
        } catch {
            print("Failed to load profiles: \(error)")
        }
    }

    func saveProfile(_ profile: AetherisProfile) async throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601
        encoder.keyEncodingStrategy = .convertToSnakeCase

        let data = try encoder.encode(profile)
        let url = profilesDirectory.appendingPathComponent("\(profile.profile.id).json")
        try data.write(to: url, options: .atomic)

        await MainActor.run {
            if let index = savedProfiles.firstIndex(where: { $0.profile.id == profile.profile.id }) {
                savedProfiles[index] = profile
            } else {
                savedProfiles.insert(profile, at: 0)
            }
            activeProfile = profile
        }
    }

    func deleteProfile(_ profile: AetherisProfile) {
        let url = profilesDirectory.appendingPathComponent("\(profile.profile.id).json")
        try? FileManager.default.removeItem(at: url)
        savedProfiles.removeAll { $0.profile.id == profile.profile.id }
        if activeProfile?.profile.id == profile.profile.id {
            activeProfile = savedProfiles.first
        }
    }
}