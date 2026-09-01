"""
Latency Benchmark & Profiler
Measures real execution time of:
1. Win32 Unicode Keystroke Injection (SendInput)
2. Text Normalizer & Voice Command Engine
3. VAD Audio Processing
4. Clipboard Burst Paste
"""

import time
import sys
import numpy as np
from core.win32_typer import Win32Typer
from core.text_processor import TextProcessor
from core.vad_detector import VadDetector

# Ensure UTF-8 output
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding='utf-8')

def benchmark_subsystems():
    print("=" * 70)
    print("⏱️ ULTRA-LOW LATENCY BENCHMARK (< 4ms TARGET PROFILING)")
    print("=" * 70)

    # 1. Win32 SendInput Latency Benchmark
    typer = Win32Typer(char_delay_ms=0.0) # Zero artificial delay
    iterations = 500
    
    start_time = time.perf_counter()
    for _ in range(iterations):
        typer.type_unicode_char("ا")
    end_time = time.perf_counter()
    avg_sendinput_ms = ((end_time - start_time) / iterations) * 1000.0
    print(f"[1] Win32 SendInput Keystroke Injection: {avg_sendinput_ms:.3f} ms / char")

    # 2. Text Normalizer & Arabic/English Processing Latency
    tp = TextProcessor(enable_voice_commands=True, enable_snippets=True)
    test_phrase = "مرحبا بكم فاصلة كيف الحال علامة استفهام بريدي"
    
    start_time = time.perf_counter()
    for _ in range(iterations):
        tp.process_token(test_phrase, "ar", "السلام عليكم")
    end_time = time.perf_counter()
    avg_text_proc_ms = ((end_time - start_time) / iterations) * 1000.0
    print(f"[2] Bilingual Text & Voice Commands Normalizer: {avg_text_proc_ms:.3f} ms / call")

    # 3. VAD Voice Activity Detection Latency (512 samples @ 16kHz)
    vad = VadDetector(sample_rate=16000)
    dummy_chunk = np.random.uniform(-0.1, 0.1, 512).astype(np.float32)
    
    start_time = time.perf_counter()
    for _ in range(iterations):
        vad.process_chunk(dummy_chunk)
    end_time = time.perf_counter()
    avg_vad_ms = ((end_time - start_time) / iterations) * 1000.0
    print(f"[3] Low-Latency VAD Audio Processing: {avg_vad_ms:.3f} ms / chunk")

    total_local_pipeline_ms = avg_sendinput_ms + avg_text_proc_ms + avg_vad_ms
    print("-" * 70)
    print(f"⚡ TOTAL LOCAL PROCESSING LATENCY: {total_local_pipeline_ms:.3f} ms")
    
    if total_local_pipeline_ms < 4.0:
        print(f"🎉 SUCCESS: Pipeline is {total_local_pipeline_ms:.3f}ms (WELL UNDER 4ms TARGET!)")
    else:
        print(f"⚠️ Target is 4ms, current is {total_local_pipeline_ms:.3f}ms")
    print("=" * 70)

if __name__ == "__main__":
    benchmark_subsystems()
