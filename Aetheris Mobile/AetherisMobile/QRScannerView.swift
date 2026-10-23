//  QRScannerView.swift
//  AetherisMobile
//  QR Code scanner with chunked QR support

import SwiftUI
import AVFoundation
import Vision

struct QRScannerView: View {
    @EnvironmentObject var appState: AppState
    @Environment(\.dismiss) private var dismiss
    @StateObject private var scanner = QRScanner()

    var body: some View {
        NavigationStack {
            ZStack {
                CameraPreview(session: scanner.session)
                    .ignoresSafeArea()

                VStack {
                    Spacer()

                    // Scanning frame overlay
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(Color.green, lineWidth: 3)
                        .frame(width: 280, height: 280)
                        .overlay(
                            VStack {
                                Spacer()
                                Text("QR-Code in den Rahmen halten")
                                    .font(.caption)
                                    .foregroundStyle(.white)
                                    .padding(.bottom, 20)
                            }
                        )

                    Spacer()

                    // Status
                    if scanner.isProcessing {
                        ProgressView("Verarbeite…")
                            .padding()
                            .background(.ultraThinMaterial)
                            .clipShape(RoundedRectangle(cornerRadius: 12))
                    } else if let error = scanner.lastError {
                        Text(error)
                            .foregroundStyle(.red)
                            .padding()
                            .background(.ultraThinMaterial)
                            .clipShape(RoundedRectangle(cornerRadius: 12))
                    }
                }
            }
            .navigationTitle("QR-Code scannen")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Abbrechen") {
                        dismiss()
                    }
                }
                ToolbarItem(placement: .primaryAction) {
                    Button {
                        scanner.toggleTorch()
                    } label: {
                        Image(systemName: scanner.torchOn ? "flashlight.on.fill" : "flashlight.off.fill")
                    }
                }
            }
            .onAppear {
                scanner.startScanning()
            }
            .onDisappear {
                scanner.stopScanning()
            }
            .onChange(of: scanner.scannedChunks) { _, chunks in
                if !chunks.isEmpty && scanner.allChunksReceived {
                    Task {
                        await processChunks(chunks)
                    }
                }
            }
        }
    }

    private func processChunks(_ chunks: [String]) async {
        do {
            let profile = try await ProfileIO.shared.importFromTransfer(
                Data(chunks.joined().utf8),
                expectedPublicKey: nil, // TODO: add public key verification
                decryptionKey: nil // TODO: add key management
            )
            await MainActor.run {
                appState.activeProfile = profile
                // Save to disk
                try? await appState.saveProfile(profile)
                dismiss()
            }
        } catch {
            await MainActor.run {
                appState.importError = error.localizedDescription
                scanner.lastError = error.localizedDescription
            }
        }
    }
}

// MARK: - Camera Preview

struct CameraPreview: UIViewRepresentable {
    let session: AVCaptureSession

    func makeUIView(context: Context) -> PreviewView {
        let view = PreviewView()
        view.videoPreviewLayer.session = session
        view.videoPreviewLayer.videoGravity = .resizeAspectFill
        return view
    }

    func updateUIView(_ uiView: PreviewView, context: Context) {}
}

final class PreviewView: UIView {
    override class var layerClass: AnyClass {
        AVCaptureVideoPreviewLayer.self
    }

    var videoPreviewLayer: AVCaptureVideoPreviewLayer {
        layer as! AVCaptureVideoPreviewLayer
    }
}

// MARK: - QR Scanner Logic

@MainActor
final class QRScanner: NSObject, ObservableObject, AVCaptureMetadataOutputObjectsDelegate {
    let session = AVCaptureSession()
    private let metadataOutput = AVCaptureMetadataOutput()
    private var chunkBuffer: [Int: String] = [:]
    private var expectedTotalChunks: Int?

    @Published var scannedChunks: [String] = []
    @Published var isProcessing = false
    @Published var lastError: String?
    @Published var torchOn = false
    var allChunksReceived: Bool {
        guard let total = expectedTotalChunks else { return false }
        return chunkBuffer.count == total
    }

    override init() {
        super.init()
        setupSession()
    }

    private func setupSession() {
        session.beginConfiguration()
        session.sessionPreset = .high

        guard let videoDevice = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: .back),
              let videoInput = try? AVCaptureDeviceInput(device: videoDevice),
              session.canAddInput(videoInput) else {
            lastError = "Kamera nicht verfügbar"
            return
        }

        session.addInput(videoInput)

        if session.canAddOutput(metadataOutput) {
            session.addOutput(metadataOutput)
            metadataOutput.setMetadataObjectsDelegate(self, queue: .main)
            metadataOutput.metadataObjectTypes = [.qr]
        }

        session.commitConfiguration()
    }

    func startScanning() {
        DispatchQueue.global(qos: .userInitiated).async {
            self.session.startRunning()
        }
    }

    func stopScanning() {
        session.stopRunning()
    }

    func toggleTorch() {
        guard let device = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: .back),
              device.hasTorch else { return }

        do {
            try device.lockForConfiguration()
            if device.torchMode == .on {
                device.torchMode = .off
                torchOn = false
            } else {
                try device.setTorchModeOn(level: 1.0)
                torchOn = true
            }
            device.unlockForConfiguration()
        } catch {
            lastError = "Taschenlampe Fehler: \(error.localizedDescription)"
        }
    }

    nonisolated func metadataOutput(_ output: AVCaptureMetadataOutput, didOutput metadataObjects: [AVMetadataObject], from connection: AVCaptureConnection) {
        guard let metadataObject = metadataObjects.first as? AVMetadataMachineReadableCodeObject,
              metadataObject.type == .qr,
              let stringValue = metadataObject.stringValue else { return }

        Task { @MainActor in
            self.handleScannedQR(stringValue)
        }
    }

    private func handleScannedQR(_ qrString: String) {
        // Check if it's a chunked QR (starts with "AETH")
        if qrString.hasPrefix("AETH") {
            handleChunkedQR(qrString)
        } else {
            // Single QR - try to parse as full profile
            handleSingleQR(qrString)
        }
    }

    private func handleSingleQR(_ jsonString: String) {
        isProcessing = true
        scannedChunks = [jsonString]
        // Will be processed by onChange in view
    }

    private func handleChunkedQR(_ chunkString: String) {
        // Parse chunk header: [magic:4][index:2][total:2][crc32:4]
        guard chunkString.count >= 12 else {
            lastError = "Ungültiges Chunk-Format"
            return
        }

        let data = Data(chunkString.utf8)
        let index = Int(data[4]) << 8 | Int(data[5])
        let total = Int(data[6]) << 8 | Int(data[7])
        let crc = data[8..<12].reduce(0) { ($0 << 8) | UInt32($1) }
        let payload = String(data: data[12...], encoding: .utf8) ?? ""

        // Verify CRC32
        let computedCRC = crc32(payload)
        if crc != computedCRC {
            lastError = "CRC-Prüfung fehlgeschlagen für Chunk \(index + 1)"
            return
        }

        expectedTotalChunks = total
        chunkBuffer[index] = payload

        // Update scanned chunks array for UI
        scannedChunks = (0..<total).compactMap { chunkBuffer[$0] }

        lastError = nil
    }

    private func crc32(_ string: String) -> UInt32 {
        var crc: UInt32 = 0xFFFFFFFF
        for byte in string.utf8 {
            crc ^= UInt32(byte)
            for _ in 0..<8 {
                crc = (crc >> 1) ^ (0xEDB88320 & -(crc & 1))
            }
        }
        return ~crc
    }
}