//  KeyboardViewController.swift
//  AetherisKeyboard
//  Custom keyboard extension with tremor assistance

import UIKit
import SwiftUI

class KeyboardViewController: UIInputViewController {

    private var hostingController: UIHostingController<KeyboardView>?
    private var profileObserver: NSObjectProtocol?

    override func viewDidLoad() {
        super.viewDidLoad()

        setupKeyboardView()
        loadProfileSettings()
        observeProfileChanges()
    }

    private func setupKeyboardView() {
        let keyboardView = KeyboardView(
            textDocumentProxy: textDocumentProxy,
            needsInputModeSwitchKey: needsInputModeSwitchKey
        )
        let hostingController = UIHostingController(rootView: keyboardView)
        self.hostingController = hostingController

        addChild(hostingController)
        view.addSubview(hostingController.view)
        hostingController.view.translatesAutoresizingMaskIntoConstraints = false
        NSLayoutConstraint.activate([
            hostingController.view.topAnchor.constraint(equalTo: view.topAnchor),
            hostingController.view.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            hostingController.view.trailingAnchor.constraint(equalTo: view.trailingAnchor),
            hostingController.view.bottomAnchor.constraint(equalTo: view.bottomAnchor)
        ])
        hostingController.didMove(toParent: self)
    }

    private func loadProfileSettings() {
        // Load from App Group shared defaults
        guard let sharedDefaults = UserDefaults(suiteName: "group.dev.aetheris.shared") else { return }

        // TODO: Load actual profile settings and apply to keyboard behavior
        // For now, just check if keyboard is enabled
        let keyboardEnabled = sharedDefaults.bool(forKey: "keyboardEnabled")
        hostingController?.rootView = KeyboardView(
            textDocumentProxy: textDocumentProxy,
            needsInputModeSwitchKey: needsInputModeSwitchKey,
            isEnabled: keyboardEnabled
        )
    }

    private func observeProfileChanges() {
        profileObserver = NotificationCenter.default.addObserver(
            forName: UserDefaults.didChangeNotification,
            object: UserDefaults(suiteName: "group.dev.aetheris.shared"),
            queue: .main
        ) { [weak self] _ in
            self?.loadProfileSettings()
        }
    }

    override func viewWillLayoutSubviews() {
        super.viewWillLayoutSubviews()
        // Handle orientation changes
    }

    deinit {
        if let observer = profileObserver {
            NotificationCenter.default.removeObserver(observer)
        }
    }
}

// MARK: - Keyboard SwiftUI View

struct KeyboardView: View {
    let textDocumentProxy: UITextDocumentProxy
    let needsInputModeSwitchKey: Bool
    var isEnabled: Bool = true

    @State private var currentText = ""
    @State private var stabilizationBuffer: [CGPoint] = []
    @State private var lastTapTime: Date?
    @State private var tapCount = 0

    // Settings (would come from shared profile)
    @AppStorage("keyboardEnabled", store: UserDefaults(suiteName: "group.dev.aetheris.shared")) private var keyboardEnabled = true
    @AppStorage("holdDurationMs", store: UserDefaults(suiteName: "group.dev.aetheris.shared")) private var holdDurationMs = 200
    @AppStorage("ignoreRepeatedTouchesMs", store: UserDefaults(suiteName: "group.dev.aetheris.shared")) private var ignoreRepeatedTouchesMs = 100
    @AppStorage("tapAssistRadius", store: UserDefaults(suiteName: "group.dev.aetheris.shared")) private var tapAssistRadius = 10.0
    @AppStorage("stabilizationEnabled", store: UserDefaults(suiteName: "group.dev.aetheris.shared")) private var stabilizationEnabled = false
    @AppStorage("stabilizationSmoothing", store: UserDefaults(suiteName: "group.dev.aetheris.shared")) private var stabilizationSmoothing = 0.7

    var body: some View {
        GeometryReader { geometry in
            VStack(spacing: 0) {
                // Suggestion bar (optional)
                if keyboardEnabled {
                    SuggestionBar(text: currentText, proxy: textDocumentProxy)
                        .frame(height: 44)
                        .background(Color(UIColor.systemGray6))
                }

                // Main keyboard area
                if keyboardEnabled {
                    CustomKeyboardLayout(
                        proxy: textDocumentProxy,
                        geometry: geometry,
                        holdDurationMs: holdDurationMs,
                        ignoreRepeatedTouchesMs: ignoreRepeatedTouchesMs,
                        tapAssistRadius: tapAssistRadius,
                        stabilizationEnabled: stabilizationEnabled,
                        stabilizationSmoothing: stabilizationSmoothing,
                        onTextChange: { text in
                            currentText = text
                        }
                    )
                } else {
                    Text("Aetheris Keyboard deaktiviert")
                        .foregroundStyle(.secondary)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                        .background(Color(UIColor.systemBackground))
                }

                // Globe key for switching keyboards
                if needsInputModeSwitchKey {
                    HStack {
                        Spacer()
                        Button(action: advanceToNextInputMode) {
                            Image(systemName: "globe")
                                .font(.title2)
                                .foregroundStyle(.primary)
                                .frame(width: 50, height: 50)
                        }
                        .background(Color(UIColor.systemGray5))
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                        .padding(.horizontal, 8)
                        Spacer()
                    }
                    .frame(height: 50)
                    .background(Color(UIColor.systemBackground))
                }
            }
        }
        .background(Color(UIColor.systemBackground))
    }

    private func advanceToNextInputMode() {
        // This needs to be called on the input view controller
        // We'll handle this via a closure or delegate
    }
}

// MARK: - Suggestion Bar

struct SuggestionBar: View {
    let text: String
    let proxy: UITextDocumentProxy

    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                // Simple suggestions based on current text
                ForEach(suggestions(for: text), id: \.self) { suggestion in
                    Button(suggestion) {
                        insertSuggestion(suggestion)
                    }
                    .font(.caption)
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(Color(UIColor.systemGray5))
                    .clipShape(Capsule())
                }
            }
            .padding(.horizontal, 8)
        }
    }

    private func suggestions(for text: String) -> [String] {
        // Simple word completions - in real app, use ML-based suggestions
        let words = text.split(separator: " ").map(String.init)
        guard let lastWord = words.last?.lowercased(), lastWord.count >= 2 else { return [] }

        let commonWords = ["the", "and", "that", "have", "for", "not", "with", "you", "this", "but", "his", "from", "they", "she", "will", "would", "there", "their", "what", "about", "which", "when", "make", "like", "time", "just", "him", "know", "take", "people", "into", "year", "your", "good", "some", "could", "them", "see", "other", "than", "then", "now", "look", "only", "come", "its", "over", "think", "also", "back", "after", "use", "two", "how", "our", "work", "first", "well", "way", "even", "new", "want", "because", "any", "these", "give", "day", "most", "us"]

        return commonWords.filter { $0.hasPrefix(lastWord) }.prefix(5).map(String.init)
    }

    private func insertSuggestion(_ suggestion: String) {
        let words = text.split(separator: " ").map(String.init)
        var newText = words.dropLast().joined(separator: " ")
        if !newText.isEmpty { newText += " " }
        newText += suggestion + " "

        proxy.insertText(newText)
    }
}

// MARK: - Custom Keyboard Layout

struct CustomKeyboardLayout: View {
    let proxy: UITextDocumentProxy
    let geometry: GeometryProxy
    let holdDurationMs: Int
    let ignoreRepeatedTouchesMs: Int
    let tapAssistRadius: Double
    let stabilizationEnabled: Bool
    let stabilizationSmoothing: Double
    let onTextChange: (String) -> Void

    @State private var keyStates: [String: KeyState] = [:]
    @GestureState private var dragLocation: CGPoint = .zero

    private let rows: [[Key]] = [
        [.character("q"), .character("w"), .character("e"), .character("r"), .character("t"), .character("z"), .character("u"), .character("i"), .character("o"), .character("p")],
        [.character("a"), .character("s"), .character("d"), .character("f"), .character("g"), .character("h"), .character("j"), .character("k"), .character("l"), .special("backspace")],
        [.shift, .character("y"), .character("x"), .character("c"), .character("v"), .character("b"), .character("n"), .character("m"), .special(","), .special(".")],
        [.special("123"), .space, .special("return")]
    ]

    var body: some View {
        VStack(spacing: 6) {
            ForEach(rows.indices, id: \.self) { rowIndex in
                HStack(spacing: 4) {
                    ForEach(rows[rowIndex]) { key in
                        KeyView(
                            key: key,
                            proxy: proxy,
                            geometry: geometry,
                            rowCount: rows[rowIndex].count,
                            holdDurationMs: holdDurationMs,
                            ignoreRepeatedTouchesMs: ignoreRepeatedTouchesMs,
                            tapAssistRadius: tapAssistRadius,
                            onAction: { action in
                                handleKeyAction(action)
                            }
                        )
                    }
                }
            }
        }
        .padding(.horizontal, 4)
        .padding(.vertical, 8)
    }

    private func handleKeyAction(_ action: KeyAction) {
        switch action {
        case .insertText(let text):
            proxy.insertText(text)
            onTextChange(proxy.documentContextBeforeInput ?? "")
        case .deleteBackward:
            proxy.deleteBackward()
            onTextChange(proxy.documentContextBeforeInput ?? "")
        case .shift:
            // Handle shift state
            break
        case .switchKeyboard:
            // Handled by parent
            break
        case .space:
            proxy.insertText(" ")
            onTextChange(proxy.documentContextBeforeInput ?? "")
        case .returnKey:
            proxy.insertText("\n")
            onTextChange(proxy.documentContextBeforeInput ?? "")
        }
    }
}

// MARK: - Key Models

enum Key: Identifiable, Hashable {
    case character(String)
    case shift
    case space
    case special(String) // "backspace", "return", "123", ",", "."
    case globe

    var id: String {
        switch self {
        case .character(let c): return "char_\(c)"
        case .shift: return "shift"
        case .space: return "space"
        case .special(let s): return "special_\(s)"
        case .globe: return "globe"
        }
    }

    var display: String {
        switch self {
        case .character(let c): return c.uppercased()
        case .shift: return "⇧"
        case .space: return "Space"
        case .special(let s):
            switch s {
            case "backspace": return "⌫"
            case "return": return "⏎"
            case "123": return "123"
            case ",": return ","
            case ".": return "."
            default: return s
            }
        case .globe: return "🌐"
        }
    }

    var isSpecial: Bool {
        if case .special = self { return true }
        if case .shift = self { return true }
        return false
    }
}

enum KeyAction {
    case insertText(String)
    case deleteBackward
    case shift
    case switchKeyboard
    case space
    case returnKey
}

struct KeyState {
    var isPressed = false
    var pressStartTime: Date?
    var tapCount = 0
    var lastTapTime: Date?
}

// MARK: - Key View

struct KeyView: View {
    let key: Key
    let proxy: UITextDocumentProxy
    let geometry: GeometryProxy
    let rowCount: Int
    let holdDurationMs: Int
    let ignoreRepeatedTouchesMs: Int
    let tapAssistRadius: Double
    let onAction: (KeyAction) -> Void

    @State private var keyState = KeyState()
    @GestureState private var isPressed = false

    private var keyWidth: CGFloat {
        let availableWidth = geometry.size.width - 4 * CGFloat(rowCount - 1) - 8
        return availableWidth / CGFloat(rowCount)
    }

    var body: some View {
        let pressGesture = LongPressGesture(minimumDuration: Double(holdDurationMs) / 1000.0)
            .sequenced(before: DragGesture(minimumDistance: 0))
            .updating($isPressed) { _, state, _ in
                state = true
            }
            .onEnded { value in
                handlePressEnded(value)
            }

        ZStack {
            RoundedRectangle(cornerRadius: 8)
                .fill(keyColor)
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(Color(UIColor.separator), lineWidth: 0.5)
                )

            Text(key.display)
                .font(keyFont)
                .foregroundStyle(keyTextColor)
                .minimumScaleFactor(0.5)
                .lineLimit(1)
        }
        .frame(width: keyWidth, height: 50)
        .scaleEffect(isPressed ? 0.95 : 1.0)
        .animation(.easeInOut(duration: 0.1), value: isPressed)
        .gesture(pressGesture)
        .simultaneousGesture(
            TapGesture()
                .onEnded {
                    handleTap()
                }
        )
    }

    private var keyColor: Color {
        if key.isSpecial {
            return Color(UIColor.systemGray5)
        }
        return Color(UIColor.systemBackground)
    }

    private var keyTextColor: Color {
        if key.isSpecial {
            return Color(UIColor.label)
        }
        return Color(UIColor.label)
    }

    private var keyFont: Font {
        if key == .space {
            return .system(size: 16, weight: .medium)
        }
        return .system(size: 20, weight: .regular)
    }

    private func handleTap() {
        let now = Date()

        // Check ignore repeated touches
        if let lastTap = keyState.lastTapTime,
           now.timeIntervalSince(lastTap) * 1000 < Double(ignoreRepeatedTouchesMs) {
            return
        }

        keyState.lastTapTime = now
        keyState.tapCount += 1

        // Determine action based on key
        switch key {
        case .character(let c):
            onAction(.insertText(c))
        case .special(let s):
            switch s {
            case "backspace":
                onAction(.deleteBackward)
            case "return":
                onAction(.returnKey)
            case "123":
                // Switch to numbers/symbols layout
                break
            case ",":
                onAction(.insertText(","))
            case ".":
                onAction(.insertText("."))
            default:
                break
            }
        case .space:
            onAction(.space)
        case .shift:
            onAction(.shift)
        case .globe:
            onAction(.switchKeyboard)
        }
    }

    private func handlePressEnded(_ value: GestureStateGesture<LongPressGesture, DragGesture>.Value) {
        // Long press handling for special keys (e.g., hold for alternatives)
        switch value {
        case .second(true, _):
            // Long press recognized - could show alternative characters
            break
        default:
            break
        }
    }
}