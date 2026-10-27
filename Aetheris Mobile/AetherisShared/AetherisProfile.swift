//  AetherisProfile.swift
//  AetherisShared
//  Shared data models matching the Aetheris JSON schema v1.0.0

import Foundation
import CryptoKit

// MARK: - Schema Version

public struct SchemaVersion: Codable, Equatable, Sendable {
    public static let current = "1.0.0"
    public let major: Int
    public let minor: Int
    public let patch: Int
    public let rawValue: String

    public init(_ string: String) throws {
        let parts = string.split(separator: ".")
        guard parts.count == 3,
              let major = Int(parts[0]),
              let minor = Int(parts[1]),
              let patch = Int(parts[2]) else {
            throw SchemaError.invalidVersionFormat(string)
        }
        self.major = major
        self.minor = minor
        self.patch = patch
        self.rawValue = string
    }

    public init(major: Int, minor: Int, patch: Int) {
        self.major = major
        self.minor = minor
        self.patch = patch
        self.rawValue = "\(major).\(minor).\(patch)"
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        let string = try container.decode(String.self)
        try self.init(string)
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(rawValue)
    }
}

public enum SchemaError: Error, LocalizedError {
    case invalidVersionFormat(String)
    case versionMismatch(expected: String, actual: String)
    case unsupportedVersion(String)

    public var errorDescription: String? {
        switch self {
        case .invalidVersionFormat(let v): return "Invalid schema version format: \(v)"
        case .versionMismatch(let expected, let actual): return "Schema version mismatch. Expected \(expected), got \(actual)"
        case .unsupportedVersion(let v): return "Unsupported schema version: \(v)"
        }
    }
}

// MARK: - Profile Identification

public struct Profile: Codable, Equatable, Sendable {
    public let id: UUID
    public let name: String
    public let createdAt: Date
    public let updatedAt: Date
    public let notes: String?
    public var encryptionEnabled: Bool = true
    public var useDpapi: Bool = true

    public init(id: UUID = UUID(), name: String, createdAt: Date = Date(), updatedAt: Date = Date(), notes: String? = nil) {
        self.id = id
        self.name = name
        self.createdAt = createdAt
        self.updatedAt = updatedAt
        self.notes = notes
    }
}

// MARK: - Measurements

public enum BodyRegion: String, Codable, CaseIterable, Sendable {
    case hand, arm, finger, leg, foot, head, voice, trunk, other
}

public enum TremorType: String, Codable, CaseIterable, Sendable {
    case resting, action, postural, kinetic, intention, taskSpecific = "task-specific", orthostatic, userDefined = "user_defined", unknown
}

public enum Amplitude: String, Codable, CaseIterable, Sendable {
    case fine, medium, coarse
}

public enum DeviceType: String, Codable, CaseIterable, Sendable {
    case accelerometer, gyroscope, smartphone, wearable, clinical, manual, other
}

public struct DeviceInfo: Codable, Equatable, Sendable {
    public let deviceType: DeviceType
    public let deviceName: String?
    public let samplingRateHz: Int?

    public init(deviceType: DeviceType, deviceName: String? = nil, samplingRateHz: Int? = nil) {
        self.deviceType = deviceType
        self.deviceName = deviceName
        self.samplingRateHz = samplingRateHz
    }
}

public struct Measurement: Codable, Equatable, Sendable, Identifiable {
    public let id: UUID
    public let bodyRegion: BodyRegion
    public let tremorType: TremorType
    public let frequencyHz: Double?
    public let amplitude: Amplitude?
    public let severity: Int? // 0-4 clinical scale
    public let measuredAt: Date
    public let conditions: String?
    public let durationSeconds: Int?
    public let deviceInfo: DeviceInfo?

    public init(
        id: UUID = UUID(),
        bodyRegion: BodyRegion,
        tremorType: TremorType = .unknown,
        frequencyHz: Double? = nil,
        amplitude: Amplitude? = nil,
        severity: Int? = nil,
        measuredAt: Date = Date(),
        conditions: String? = nil,
        durationSeconds: Int? = nil,
        deviceInfo: DeviceInfo? = nil
    ) {
        self.id = id
        self.bodyRegion = bodyRegion
        self.tremorType = tremorType
        self.frequencyHz = frequencyHz
        self.amplitude = amplitude
        self.severity = severity
        self.measuredAt = measuredAt
        self.conditions = conditions
        self.durationSeconds = durationSeconds
        self.deviceInfo = deviceInfo
    }
}

// MARK: - Findings

public enum FindingCategory: String, Codable, CaseIterable, Sendable {
    case symptom, trigger, relief, activityImpact = "activity_impact", medicationEffect = "medication_effect", custom, unknown
}

public struct Finding: Codable, Equatable, Sendable, Identifiable {
    public let id: UUID
    public let category: FindingCategory
    public let description: String
    public let bodyRegion: BodyRegion?
    public let severity: Int? // 0-10 subjective
    public let recordedAt: Date
    public let tags: [String]

    public init(
        id: UUID = UUID(),
        category: FindingCategory = .unknown,
        description: String,
        bodyRegion: BodyRegion? = nil,
        severity: Int? = nil,
        recordedAt: Date = Date(),
        tags: [String] = []
    ) {
        self.id = id
        self.category = category
        self.description = description
        self.bodyRegion = bodyRegion
        self.severity = severity
        self.recordedAt = recordedAt
        self.tags = tags
    }
}

// MARK: - Settings

public struct TouchAssistSettings: Codable, Equatable, Sendable {
    public var enabled: Bool = false
    public var holdDurationMs: Int? // 50-3000
    public var ignoreRepeatedTouchesMs: Int? // 0-2000
    public var tapAssistRadius: Double? // 0-50 points
    public var stabilizationEnabled: Bool = false
    public var stabilizationSmoothing: Double? // 0.0-1.0

    public init() {}
}

public struct MotionAssistSettings: Codable, Equatable, Sendable {
    public var enabled: Bool = false
    public var reduceMotion: Bool = true
    public var autoRotateDisabled: Bool = false
    public var shakeToUndoDisabled: Bool = true

    public init() {}
}

public struct VoiceAssistSettings: Codable, Equatable, Sendable {
    public var enabled: Bool = false
    public var voiceControlEnabled: Bool = false
    public var dictationEnabled: Bool = false

    public init() {}
}

public struct DisplayAssistSettings: Codable, Equatable, Sendable {
    public var enabled: Bool = false
    public var largerText: Bool = false
    public var boldText: Bool = false
    public var increaseContrast: Bool = false
    public var reduceTransparency: Bool = false
    public var reduceWhitePoint: Bool = false

    public init() {}
}

public struct Settings: Codable, Equatable, Sendable {
    public var version: Int = 1
    public var touchAssist: TouchAssistSettings?
    public var motionAssist: MotionAssistSettings?
    public var voiceAssist: VoiceAssistSettings?
    public var displayAssist: DisplayAssistSettings?
    public var custom: [String: CustomValue] = [:]

    public init() {}
}

// Custom value type for extensible settings (JSON primitives only)
public enum CustomValue: Codable, Equatable, Sendable {
    case string(String)
    case number(Double)
    case bool(Bool)
    case null

    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let bool = try? container.decode(Bool.self) {
            self = .bool(bool)
        } else if let int = try? container.decode(Int.self) {
            self = .number(Double(int))
        } else if let double = try? container.decode(Double.self) {
            self = .number(double)
        } else if let string = try? container.decode(String.self) {
            self = .string(string)
        } else {
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "CustomValue must be string, number, bool, or null")
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .string(let s): try container.encode(s)
        case .number(let n): try container.encode(n)
        case .bool(let b): try container.encode(b)
        case .null: try container.encodeNil()
        }
    }
}

// MARK: - Transfer Metadata

public struct IntegrityInfo: Codable, Equatable, Sendable {
    public let algorithm: String // "SHA-256"
    public let hash: String // 64 hex chars

    public init(algorithm: String = "SHA-256", hash: String) {
        self.algorithm = algorithm
        self.hash = hash
    }
}

public struct SignatureInfo: Codable, Equatable, Sendable {
    public let algorithm: String // "Ed25519"
    public let publicKey: String // base64
    public let signature: String // base64

    public init(algorithm: String = "Ed25519", publicKey: String, signature: String) {
        self.algorithm = algorithm
        self.publicKey = publicKey
        self.signature = signature
    }
}

public struct TransferInfo: Codable, Equatable, Sendable {
    public let exportedAt: Date
    public let exportedBy: String // "Aetheris.Studio"
    public let exportVersion: String
    public let integrity: IntegrityInfo
    public let signature: SignatureInfo?
    public let encrypted: Bool
    public let payload: String? // base64 ciphertext if encrypted

    public init(
        exportedAt: Date = Date(),
        exportedBy: String = "Aetheris.Studio",
        exportVersion: String = "1.0.0",
        integrity: IntegrityInfo,
        signature: SignatureInfo? = nil,
        encrypted: Bool = false,
        payload: String? = nil
    ) {
        self.exportedAt = exportedAt
        self.exportedBy = exportedBy
        self.exportVersion = exportVersion
        self.integrity = integrity
        self.signature = signature
        self.encrypted = encrypted
        self.payload = payload
    }
}

// MARK: - Root Profile

public struct AetherisProfile: Codable, Equatable, Sendable {
    public let schemaVersion: SchemaVersion
    public let profile: Profile
    public let measurements: [Measurement]
    public let findings: [Finding]
    public let settings: Settings
    public let transfer: TransferInfo?

    public init(
        schemaVersion: SchemaVersion = try! SchemaVersion(SchemaVersion.current),
        profile: Profile,
        measurements: [Measurement] = [],
        findings: [Finding] = [],
        settings: Settings = Settings(),
        transfer: TransferInfo? = nil
    ) {
        self.schemaVersion = schemaVersion
        self.profile = profile
        self.measurements = measurements
        self.findings = findings
        self.settings = settings
        self.transfer = transfer
    }
}

// MARK: - Validation

public enum ValidationError: Error, LocalizedError, Equatable, Sendable {
    case missingField(String)
    case invalidRange(field: String, value: String, expected: String)
    case invalidFormat(field: String, expected: String)
    case businessRule(String)
    case integrityFailed(String)
    case signatureInvalid
    case missingTransfer
    case encryptionRequired
    case decryptionFailed

    public var errorDescription: String? {
        switch self {
        case .missingField(let f): return "Missing required field: \(f)"
        case .invalidRange(let f, let v, let e): return "\(f): value '\(v)' not in range \(e)"
        case .invalidFormat(let f, let e): return "\(f): invalid format, expected \(e)"
        case .businessRule(let msg): return msg
        case .integrityFailed(let msg): return "Integrity check failed: \(msg)"
        case .signatureInvalid: return "Digital signature verification failed"
        case .missingTransfer: return "Transfer metadata missing"
        case .encryptionRequired: return "Profile is encrypted but no decryption key provided"
        case .decryptionFailed: return "Failed to decrypt profile payload"
        }
    }
}

public struct ValidationResult: Equatable, Sendable {
    public let isValid: Bool
    public let errors: [ValidationError]

    public static let success = ValidationResult(isValid: true, errors: [])
    public static func failure(_ errors: [ValidationError]) -> ValidationResult {
        ValidationResult(isValid: false, errors: errors)
    }
    public static func failure(_ error: ValidationError) -> ValidationResult {
        ValidationResult(isValid: false, errors: [error])
    }
}

public actor ProfileValidator {
    public static let shared = ProfileValidator()

    private init() {}

    public func validate(_ profile: AetherisProfile) -> ValidationResult {
        var errors: [ValidationError] = []

        // Schema version
        if profile.schemaVersion.rawValue != SchemaVersion.current {
            errors.append(.businessRule("Schema version '\(profile.schemaVersion.rawValue)' not supported. Current: \(SchemaVersion.current)"))
        }

        // Profile
        if profile.profile.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            errors.append(.missingField("profile.name"))
        }
        if profile.profile.updatedAt < profile.profile.createdAt {
            errors.append(.businessRule("profile.updatedAt cannot be before profile.createdAt"))
        }

        // Measurements
        for (index, m) in profile.measurements.enumerated() {
            let prefix = "measurements[\(index)]"
            if let freq = m.frequencyHz, !(0.5...30).contains(freq) {
                errors.append(.invalidRange(field: "\(prefix).frequencyHz", value: "\(freq)", expected: "0.5-30"))
            }
            if let sev = m.severity, !(0...4).contains(sev) {
                errors.append(.invalidRange(field: "\(prefix).severity", value: "\(sev)", expected: "0-4"))
            }
            if let dur = m.durationSeconds, !(1...3600).contains(dur) {
                errors.append(.invalidRange(field: "\(prefix).durationSeconds", value: "\(dur)", expected: "1-3600"))
            }
            if let rate = m.deviceInfo?.samplingRateHz, !(10...1000).contains(rate) {
                errors.append(.invalidRange(field: "\(prefix).deviceInfo.samplingRateHz", value: "\(rate)", expected: "10-1000"))
            }
        }

        // Findings
        for (index, f) in profile.findings.enumerated() {
            let prefix = "findings[\(index)]"
            if f.description.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                errors.append(.missingField("\(prefix).description"))
            }
            if let sev = f.severity, !(0...10).contains(sev) {
                errors.append(.invalidRange(field: "\(prefix).severity", value: "\(sev)", expected: "0-10"))
            }
            if f.tags.count > 10 {
                errors.append(.businessRule("\(prefix).tags: maximum 10 tags allowed"))
            }
        }

        // Settings
        if profile.settings.version < 1 {
            errors.append(.invalidRange(field: "settings.version", value: "\(profile.settings.version)", expected: ">=1"))
        }
        if let touch = profile.settings.touchAssist {
            if let h = touch.holdDurationMs, !(50...3000).contains(h) {
                errors.append(.invalidRange(field: "settings.touchAssist.holdDurationMs", value: "\(h)", expected: "50-3000"))
            }
            if let r = touch.ignoreRepeatedTouchesMs, !(0...2000).contains(r) {
                errors.append(.invalidRange(field: "settings.touchAssist.ignoreRepeatedTouchesMs", value: "\(r)", expected: "0-2000"))
            }
            if let r = touch.tapAssistRadius, !(0...50).contains(r) {
                errors.append(.invalidRange(field: "settings.touchAssist.tapAssistRadius", value: "\(r)", expected: "0-50"))
            }
            if let s = touch.stabilizationSmoothing, !(0.0...1.0).contains(s) {
                errors.append(.invalidRange(field: "settings.touchAssist.stabilizationSmoothing", value: "\(s)", expected: "0.0-1.0"))
            }
        }

        return errors.isEmpty ? .success : .failure(errors)
    }

    public func validateTransfer(_ profile: AetherisProfile, expectedPublicKey: String? = nil) -> ValidationResult {
        var errors: [ValidationError] = []

        guard let transfer = profile.transfer else {
            return .failure(.missingTransfer)
        }

        // Recompute hash
        let profileForHash = AetherisProfile(
            schemaVersion: profile.schemaVersion,
            profile: profile.profile,
            measurements: profile.measurements,
            findings: profile.findings,
            settings: profile.settings,
            transfer: nil
        )

        let canonicalJSON = canonicalJSONData(profileForHash)
        let computedHash = SHA256.hash(data: canonicalJSON).compactMap { String(format: "%02x", $0) }.joined()

        if computedHash.lowercased() != transfer.integrity.hash.lowercased() {
            errors.append(.integrityFailed("Hash mismatch - profile data may have been modified"))
        }

        // Verify signature if present
        if let sig = transfer.signature {
            guard let publicKey = expectedPublicKey else {
                errors.append(.businessRule("Signature present but no public key provided for verification"))
                return .failure(errors)
            }
            // Ed25519 verification would go here
            // For now, check format
            if !isValidBase64(publicKey, expectedLength: 32) || !isValidBase64(sig.signature, expectedLength: 64) {
                errors.append(.signatureInvalid)
            }
        }

        return errors.isEmpty ? .success : .failure(errors)
    }

    private func canonicalJSONData(_ profile: AetherisProfile) -> Data {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601
        encoder.keyEncodingStrategy = .convertToSnakeCase
        return (try? encoder.encode(profile)) ?? Data()
    }

    private func isValidBase64(_ string: String, expectedLength: Int) -> Bool {
        guard let data = Data(base64Encoded: string) else { return false }
        return data.count == expectedLength
    }
}

// MARK: - Profile I/O

public actor ProfileIO {
    public static let shared = ProfileIO()
    private let validator = ProfileValidator.shared

    private init() {}

    public func load(from url: URL) async throws -> AetherisProfile {
        let data = try Data(contentsOf: url)
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let profile = try decoder.decode(AetherisProfile.self, from: data)

        let result = await validator.validate(profile)
        if !result.isValid {
            throw ValidationError.businessRule(result.errors.map(\.localizedDescription).joined(separator: "; "))
        }
        return profile
    }

    public func save(_ profile: AetherisProfile, to url: URL) async throws {
        // Note: Profile is immutable, so we'd need a builder pattern or mutable copy
        // For now, assume caller handles updatedAt

        let result = await validator.validate(profile)
        if !result.isValid {
            throw ValidationError.businessRule(result.errors.map(\.localizedDescription).joined(separator: "; "))
        }

        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let data = try encoder.encode(profile)
        try data.write(to: url, options: .atomic)
    }

    public func prepareForTransfer(
        _ profile: AetherisProfile,
        encrypt: Bool = false,
        encryptionKey: SymmetricKey? = nil,
        signingPrivateKey: Curve25519.Signing.PrivateKey? = nil
    ) async throws -> AetherisProfile {
        let result = await validator.validate(profile)
        if !result.isValid {
            throw ValidationError.businessRule(result.errors.map(\.localizedDescription).joined(separator: "; "))
        }

        // Compute integrity hash
        let profileForHash = AetherisProfile(
            schemaVersion: profile.schemaVersion,
            profile: profile.profile,
            measurements: profile.measurements,
            findings: profile.findings,
            settings: profile.settings,
            transfer: nil
        )

        let canonicalData = canonicalJSONData(profileForHash)
        let hash = SHA256.hash(data: canonicalData).compactMap { String(format: "%02x", $0) }.joined()

        var transfer = TransferInfo(
            integrity: IntegrityInfo(hash: hash),
            encrypted: encrypt
        )

        var finalProfile = profile

        if encrypt {
            guard let key = encryptionKey else {
                throw ValidationError.encryptionRequired
            }
            let sealedBox = try AES.GCM.seal(canonicalData, using: key)
            let combined = sealedBox.nonce + sealedBox.ciphertext + sealedBox.tag
            transfer = TransferInfo(
                exportedAt: transfer.exportedAt,
                exportedBy: transfer.exportedBy,
                exportVersion: transfer.exportVersion,
                integrity: transfer.integrity,
                signature: transfer.signature,
                encrypted: true,
                payload: combined.base64EncodedString()
            )
        }

        if let privateKey = signingPrivateKey {
            let signature = try privateKey.signature(for: canonicalData)
            transfer = TransferInfo(
                exportedAt: transfer.exportedAt,
                exportedBy: transfer.exportedBy,
                exportVersion: transfer.exportVersion,
                integrity: transfer.integrity,
                signature: SignatureInfo(
                    publicKey: privateKey.publicKey.rawRepresentation.base64EncodedString(),
                    signature: signature.base64EncodedString()
                ),
                encrypted: transfer.encrypted,
                payload: transfer.payload
            )
        }

        finalProfile = AetherisProfile(
            schemaVersion: profile.schemaVersion,
            profile: profile.profile,
            measurements: profile.measurements,
            findings: profile.findings,
            settings: profile.settings,
            transfer: transfer
        )

        return finalProfile
    }

    public func importFromTransfer(
        _ data: Data,
        expectedPublicKey: Curve25519.Signing.PublicKey? = nil,
        decryptionKey: SymmetricKey? = nil
    ) async throws -> AetherisProfile {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        decoder.keyDecodingStrategy = .convertFromSnakeCase

        var profile: AetherisProfile

        do {
            profile = try decoder.decode(AetherisProfile.self, from: data)
        } catch {
            // Try base64 decode
            guard let jsonString = String(data: data, encoding: .utf8),
                  let decodedData = Data(base64Encoded: jsonString) else {
                throw ValidationError.invalidFormat(field: "root", expected: "valid JSON or base64")
            }
            profile = try decoder.decode(AetherisProfile.self, from: decodedData)
        }

        // Handle encrypted payload
        if profile.transfer?.encrypted == true {
            guard let key = decryptionKey else {
                throw ValidationError.encryptionRequired
            }
            guard let payloadB64 = profile.transfer?.payload,
                  let combined = Data(base64Encoded: payloadB64),
                  combined.count >= 12 + 16 else {
                throw ValidationError.decryptionFailed
            }

            let nonce = combined[0..<12]
            let tag = combined[(combined.count - 16)...]
            let ciphertext = combined[12..<(combined.count - 16)]

            let sealedBox = try AES.GCM.SealedBox(
                nonce: try AES.GCM.Nonce(data: nonce),
                ciphertext: ciphertext,
                tag: try AES.GCM.Tag(data: tag)
            )
            let plaintext = try AES.GCM.open(sealedBox, using: key)

            let decryptedProfile = try decoder.decode(AetherisProfile.self, from: plaintext)
            profile = AetherisProfile(
                schemaVersion: decryptedProfile.schemaVersion,
                profile: decryptedProfile.profile,
                measurements: decryptedProfile.measurements,
                findings: decryptedProfile.findings,
                settings: decryptedProfile.settings,
                transfer: profile.transfer
            )
        }

        // Validate structure
        let validation = await validator.validate(profile)
        if !validation.isValid {
            throw ValidationError.businessRule(validation.errors.map(\.localizedDescription).joined(separator: "; "))
        }

        // Validate transfer integrity
        let transferValidation = await validator.validateTransfer(profile, expectedPublicKey: expectedPublicKey?.rawRepresentation.base64EncodedString())
        if !transferValidation.isValid {
            throw ValidationError.businessRule(transferValidation.errors.map(\.localizedDescription).joined(separator: "; "))
        }

        return profile
    }

    private func canonicalJSONData(_ profile: AetherisProfile) -> Data {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601
        encoder.keyEncodingStrategy = .convertToSnakeCase
        return (try? encoder.encode(profile)) ?? Data()
    }
}