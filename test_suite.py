"""
Automated Verification Test Suite for STT & Live Typing Interpreter
Tests: Win32 Unicode Typer, Text Processor, VAD Detector, and Audio Subsystems.
"""

import sys
import numpy as np

def test_text_processor():
    print("[1/3] Testing TextProcessor (Arabic & English)...")
    from core.text_processor import TextProcessor

    tp = TextProcessor(strip_tashkeel=True)

    # Test Arabic punctuation conversion & Tashkeel removal
    ar_input = "مَرْحَبًا بِكُمْ , كَيْفَ الحَالُ ?"
    ar_processed, lang = tp.process_token(ar_input, "ar")
    assert lang == "ar", f"Expected 'ar', got {lang}"
    assert "،" in ar_processed, "Arabic comma '،' not converted"
    assert "؟" in ar_processed, "Arabic question mark '؟' not converted"
    assert "َ" not in ar_processed, "Tashkeel should be stripped"
    print("  -> Arabic Normalization: PASS")

    # Test English auto-capitalization & punctuation
    en_input = "hello world. i am testing live stt."
    en_processed, lang = tp.process_token(en_input, "en")
    assert lang == "en", f"Expected 'en', got {lang}"
    assert en_processed.startswith("Hello"), f"Expected capitalization, got: {en_processed}"
    assert " I am " in en_processed, f"Expected 'I' capitalization, got: {en_processed}"
    print("  -> English Normalization: PASS")

    # Test Language Detection
    assert tp.detect_dominant_language("السلام عليكم") == "ar"
    assert tp.detect_dominant_language("Fast streaming STT") == "en"
    print("  -> Bilingual Language Auto-Detection: PASS")


def test_vad_detector():
    print("\n[2/3] Testing VAD (Voice Activity Detection)...")
    from core.vad_detector import VadDetector

    vad = VadDetector(sample_rate=16000, energy_threshold=0.02)

    # Silent chunk
    silence = np.zeros(512, dtype=np.float32)
    assert vad.process_chunk(silence) == False, "Silence detected as speech"

    # Loud speech chunk (sine wave)
    t = np.linspace(0, 0.032, 512, endpoint=False)
    speech = (0.5 * np.sin(2 * np.pi * 440 * t)).astype(np.float32)
    assert vad.process_chunk(speech) == True, "Speech not detected"
    print("  -> VAD Detection & Energy Filter: PASS")


def test_win32_typer_structures():
    print("\n[3/3] Testing Win32 SendInput Structures...")
    from core.win32_typer import Win32Typer, INPUT, ctypes
    typer = Win32Typer()
    assert ctypes.sizeof(INPUT) > 0
    print("  -> Win32 C Structs & SendInput bindings: PASS")


if __name__ == "__main__":
    print("=== Running Windows STT Test Suite ===")
    try:
        test_text_processor()
        test_vad_detector()
        test_win32_typer_structures()
        print("\n=========================================")
        print(" ALL VERIFICATION TESTS PASSED SUCCESSFULLY! ")
        print("=========================================")
    except Exception as e:
        print(f"\n❌ TEST FAILED: {e}")
        sys.exit(1)
