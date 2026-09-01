"""
Voice Commands & Formatting Engine
Translates spoken punctuation and formatting commands in Arabic & English into actual symbols.
"""

import re
from typing import Tuple

class VoiceCommandParser:
    def __init__(self, enabled: bool = True):
        self.enabled = enabled

        # Arabic voice punctuation commands
        self.arabic_commands = [
            (r'\b(فاصلة|فاصله)\b', '،'),
            (r'\b(فاصلة منقوطة|فاصله منقوطه)\b', '؛'),
            (r'\b(نقطة|نقطه|نهاية الجملة)\b', '.'),
            (r'\b(علامة استفهام|علامه استفهام)\b', '؟'),
            (r'\b(علامة تعجب|علامه تعجب)\b', '!'),
            (r'\b(سطر جديد|انزل سطر)\b', '\n'),
            (r'\b(فقرة جديدة|فقره جديده)\b', '\n\n'),
            (r'\b(نقطتان|نقطتين فوق بعض)\b', ':'),
            (r'\b(افتح قوس|قوس مفتوح)\b', ' ('),
            (r'\b(اغلق قوس|قوس مغلق|اقفل القوس)\b', ') '),
            (r'\b(شرطة|شرطه)\b', ' - '),
            (r'\b(علامة اقتباس|افتح اقتباس)\b', ' "'),
            (r'\b(اغلق اقتباس|اقفل اقتباس)\b', '" ')
        ]

        # English voice punctuation commands
        self.english_commands = [
            (r'\b(comma)\b', ','),
            (r'\b(period|full stop|dot)\b', '.'),
            (r'\b(question mark)\b', '?'),
            (r'\b(exclamation mark|exclamation point)\b', '!'),
            (r'\b(new line|enter)\b', '\n'),
            (r'\b(new paragraph)\b', '\n\n'),
            (r'\b(colon)\b', ':'),
            (r'\b(semicolon)\b', ';'),
            (r'\b(open parenthesis|open bracket)\b', ' ('),
            (r'\b(close parenthesis|close bracket)\b', ') '),
            (r'\b(hyphen|dash)\b', ' - '),
            (r'\b(open quote)\b', ' "'),
            (r'\b(close quote)\b', '" ')
        ]

    def parse(self, text: str, lang: str = 'auto') -> str:
        """
        Parses text and replaces spoken formatting commands with symbols.
        """
        if not self.enabled or not text:
            return text

        result = text

        # Apply Arabic commands
        if lang in ['ar', 'auto']:
            for pattern, replacement in self.arabic_commands:
                result = re.sub(pattern, replacement, result, flags=re.IGNORECASE)

        # Apply English commands
        if lang in ['en', 'auto']:
            for pattern, replacement in self.english_commands:
                result = re.sub(pattern, replacement, result, flags=re.IGNORECASE)

        return result
