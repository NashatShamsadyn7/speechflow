"""
Global Hotkey Manager
Handles system-wide keyboard shortcuts for toggling dictation and switching languages.
"""

import threading
from typing import Callable, Optional

try:
    import keyboard
except ImportError:
    keyboard = None

try:
    from pynput import keyboard as pynput_kb
except ImportError:
    pynput_kb = None


class HotkeyManager:
    def __init__(
        self,
        toggle_hotkey: str = "ctrl+shift+space",
        lang_switch_hotkey: str = "ctrl+shift+l",
        on_toggle_dictation: Optional[Callable[[], None]] = None,
        on_switch_language: Optional[Callable[[], None]] = None
    ):
        self.toggle_hotkey = toggle_hotkey
        self.lang_switch_hotkey = lang_switch_hotkey
        self.on_toggle_dictation = on_toggle_dictation
        self.on_switch_language = on_switch_language
        self._is_active = False

    def start(self):
        """Starts listening for global hotkeys."""
        if self._is_active:
            return
        self._is_active = True

        if keyboard:
            try:
                keyboard.add_hotkey(self.toggle_hotkey, self._trigger_toggle)
                keyboard.add_hotkey(self.lang_switch_hotkey, self._trigger_lang_switch)
                print(f"[HotkeyManager] Registered hotkeys: Toggle='{self.toggle_hotkey}', Switch Lang='{self.lang_switch_hotkey}'")
                return
            except Exception as e:
                print(f"[HotkeyManager] keyboard hook failed, falling back to pynput: {e}")

        # Fallback to pynput if keyboard fails
        if pynput_kb:
            self._start_pynput_listener()

    def _trigger_toggle(self):
        if self.on_toggle_dictation:
            threading.Thread(target=self.on_toggle_dictation, daemon=True).start()

    def _trigger_lang_switch(self):
        if self.on_switch_language:
            threading.Thread(target=self.on_switch_language, daemon=True).start()

    def _start_pynput_listener(self):
        # Basic combination detector via pynput
        current_keys = set()

        def on_press(key):
            current_keys.add(key)
            # Check for ctrl+shift+space
            ctrl_down = any(k in current_keys for k in [pynput_kb.Key.ctrl, pynput_kb.Key.ctrl_l, pynput_kb.Key.ctrl_r])
            shift_down = any(k in current_keys for k in [pynput_kb.Key.shift, pynput_kb.Key.shift_l, pynput_kb.Key.shift_r])
            
            if ctrl_down and shift_down:
                if key == pynput_kb.Key.space:
                    self._trigger_toggle()
                elif hasattr(key, 'char') and key.char in ['l', 'L']:
                    self._trigger_lang_switch()

        def on_release(key):
            current_keys.discard(key)

        listener = pynput_kb.Listener(on_press=on_press, on_release=on_release)
        listener.daemon = True
        listener.start()

    def stop(self):
        """Cleans up registered hotkeys."""
        self._is_active = False
        if keyboard:
            try:
                keyboard.unhook_all_hotkeys()
            except Exception:
                pass
