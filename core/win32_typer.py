"""
Enhanced Win32 SendInput Unicode Typer for Windows
Types text directly into the focused window using native Windows Unicode injection.
Supports character-by-character live typing, atomic clipboard fast-paste, and instant layout-independent RTL typing.
"""

import ctypes
import time
from typing import Optional

# Win32 Constants
INPUT_KEYBOARD = 1
KEYEVENTF_EXTENDEDKEY = 0x0001
KEYEVENTF_KEYUP = 0x0002
KEYEVENTF_UNICODE = 0x0004
KEYEVENTF_SCANCODE = 0x0008

VK_BACK = 0x08
VK_RETURN = 0x0D
VK_CONTROL = 0x11
VK_V = 0x56

class KEYBDINPUT(ctypes.Structure):
    _fields_ = [
        ("wVk", ctypes.c_ushort),
        ("wScan", ctypes.c_ushort),
        ("dwFlags", ctypes.c_ulong),
        ("time", ctypes.c_ulong),
        ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong)),
    ]

class HARDWAREINPUT(ctypes.Structure):
    _fields_ = [
        ("uMsg", ctypes.c_ulong),
        ("wParamL", ctypes.c_short),
        ("wParamH", ctypes.c_ushort),
    ]

class MOUSEINPUT(ctypes.Structure):
    _fields_ = [
        ("dx", ctypes.c_long),
        ("dy", ctypes.c_long),
        ("mouseData", ctypes.c_ulong),
        ("dwFlags", ctypes.c_ulong),
        ("time", ctypes.c_ulong),
        ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong)),
    ]

class _INPUT_UNION(ctypes.Union):
    _fields_ = [
        ("mi", MOUSEINPUT),
        ("ki", KEYBDINPUT),
        ("hi", HARDWAREINPUT),
    ]

class INPUT(ctypes.Structure):
    _fields_ = [
        ("type", ctypes.c_ulong),
        ("union", _INPUT_UNION),
    ]

user32 = ctypes.windll.user32
SendInput = user32.SendInput
SendInput.argtypes = (ctypes.c_uint, ctypes.POINTER(INPUT), ctypes.c_int)
SendInput.restype = ctypes.c_uint


class Win32Typer:
    def __init__(self, char_delay_ms: float = 1.0):
        self.char_delay = char_delay_ms / 1000.0

    def set_speed(self, char_delay_ms: float):
        self.char_delay = max(0.0, char_delay_ms / 1000.0)

    def type_unicode_char(self, char: str):
        """Send a single character via KEYEVENTF_UNICODE."""
        if char == '\n':
            # Handle Enter/New line
            self.send_enter()
            return

        utf16_units = char.encode('utf-16le')
        for i in range(0, len(utf16_units), 2):
            code_unit = int.from_bytes(utf16_units[i:i+2], byteorder='little')
            
            inp_down = INPUT()
            inp_down.type = INPUT_KEYBOARD
            inp_down.union.ki.wVk = 0
            inp_down.union.ki.wScan = code_unit
            inp_down.union.ki.dwFlags = KEYEVENTF_UNICODE

            inp_up = INPUT()
            inp_up.type = INPUT_KEYBOARD
            inp_up.union.ki.wVk = 0
            inp_up.union.ki.wScan = code_unit
            inp_up.union.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP

            inputs = (INPUT * 2)(inp_down, inp_up)
            SendInput(2, inputs, ctypes.sizeof(INPUT))
            
            if self.char_delay > 0:
                time.sleep(self.char_delay)

    def type_text(self, text: str):
        """Type full string character by character."""
        for char in text:
            self.type_unicode_char(char)

    def send_enter(self):
        """Send Enter key event."""
        down = INPUT()
        down.type = INPUT_KEYBOARD
        down.union.ki.wVk = VK_RETURN

        up = INPUT()
        up.type = INPUT_KEYBOARD
        up.union.ki.wVk = VK_RETURN
        up.union.ki.dwFlags = KEYEVENTF_KEYUP

        inputs = (INPUT * 2)(down, up)
        SendInput(2, inputs, ctypes.sizeof(INPUT))

    def send_backspaces(self, count: int):
        """Send N backspace keystrokes."""
        if count <= 0:
            return
        inputs = []
        for _ in range(count):
            down = INPUT()
            down.type = INPUT_KEYBOARD
            down.union.ki.wVk = VK_BACK

            up = INPUT()
            up.type = INPUT_KEYBOARD
            up.union.ki.wVk = VK_BACK
            up.union.ki.dwFlags = KEYEVENTF_KEYUP
            inputs.extend([down, up])

        arr = (INPUT * len(inputs))(*inputs)
        SendInput(len(inputs), arr, ctypes.sizeof(INPUT))

    def paste_text_fast(self, text: str, restore_clipboard: bool = True):
        """
        Fast paste method via Windows Clipboard + Ctrl+V.
        Safely saves and restores previous clipboard state.
        """
        import win32clipboard
        import win32con

        old_clipboard = None
        if restore_clipboard:
            try:
                win32clipboard.OpenClipboard()
                if win32clipboard.IsClipboardFormatAvailable(win32con.CF_UNICODETEXT):
                    old_clipboard = win32clipboard.GetClipboardData(win32con.CF_UNICODETEXT)
                win32clipboard.CloseClipboard()
            except Exception:
                pass

        # Set target text
        try:
            win32clipboard.OpenClipboard()
            win32clipboard.EmptyClipboard()
            win32clipboard.SetClipboardText(text, win32con.CF_UNICODETEXT)
            win32clipboard.CloseClipboard()
        except Exception:
            return

        # Send Ctrl + V
        ctrl_down = INPUT()
        ctrl_down.type = INPUT_KEYBOARD
        ctrl_down.union.ki.wVk = VK_CONTROL

        v_down = INPUT()
        v_down.type = INPUT_KEYBOARD
        v_down.union.ki.wVk = VK_V

        v_up = INPUT()
        v_up.type = INPUT_KEYBOARD
        v_up.union.ki.wVk = VK_V
        v_up.union.ki.dwFlags = KEYEVENTF_KEYUP

        ctrl_up = INPUT()
        ctrl_up.type = INPUT_KEYBOARD
        ctrl_up.union.ki.wVk = VK_CONTROL
        ctrl_up.union.ki.dwFlags = KEYEVENTF_KEYUP

        inputs = (INPUT * 4)(ctrl_down, v_down, v_up, ctrl_up)
        SendInput(4, inputs, ctypes.sizeof(INPUT))

        # Restore previous clipboard after 30ms
        if restore_clipboard and old_clipboard is not None:
            time.sleep(0.03)
            try:
                win32clipboard.OpenClipboard()
                win32clipboard.EmptyClipboard()
                win32clipboard.SetClipboardText(old_clipboard, win32con.CF_UNICODETEXT)
                win32clipboard.CloseClipboard()
            except Exception:
                pass
