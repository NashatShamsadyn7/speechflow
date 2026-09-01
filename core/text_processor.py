"""
Text Processor & Normalizer for Bilingual (Arabic & English) STT
Integrates voice commands, custom snippet expansion, profanity masking, Tashkeel control, and smart formatting.
"""

import re
from typing import Tuple, Optional
from core.voice_commands import VoiceCommandParser
from core.snippet_manager import SnippetManager

class TextProcessor:
    def __init__(
        self,
        strip_tashkeel: bool = False,
        enable_voice_commands: bool = True,
        enable_snippets: bool = True,
        enable_profanity_filter: bool = False,
        snippet_manager: Optional[SnippetManager] = None
    ):
        self.strip_tashkeel = strip_tashkeel
        self.enable_voice_commands = enable_voice_commands
        self.enable_snippets = enable_snippets
        self.enable_profanity_filter = enable_profanity_filter

        self.voice_parser = VoiceCommandParser(enabled=enable_voice_commands)
        self.snippet_mgr = snippet_manager or SnippetManager()

        # Arabic Tashkeel regex range
        self.tashkeel_regex = re.compile(r'[\u0617-\u061A\u064B-\u0652]')
        
        # Punctuation replacements
        self.arabic_punctuation_map = {
            ',': '،',
            '?': '؟',
            ';': '؛'
        }

        # Profanity words list for optional filtering
        self.profanity_list = [
            # Common placeholder profane tokens
            "stupid", "idiot", "damn", "غبي", "احمق", "سافل"
        ]

    def detect_dominant_language(self, text: str) -> str:
        """Detects whether text is predominantly Arabic or English/Latin."""
        arabic_chars = len(re.findall(r'[\u0600-\u06FF]', text))
        latin_chars = len(re.findall(r'[a-zA-Z]', text))
        if arabic_chars >= latin_chars and arabic_chars > 0:
            return 'ar'
        return 'en'

    def mask_profanity(self, text: str) -> str:
        if not self.enable_profanity_filter:
            return text
        for word in self.profanity_list:
            pattern = re.compile(r'\b' + re.escape(word) + r'\b', re.IGNORECASE)
            text = pattern.sub('*' * len(word), text)
        return text

    def normalize_arabic(self, text: str) -> str:
        """Applies Arabic specific formatting rules."""
        if not text:
            return ""

        if self.strip_tashkeel:
            text = self.tashkeel_regex.sub('', text)

        # Standardize Arabic punctuation
        for en_punc, ar_punc in self.arabic_punctuation_map.items():
            text = text.replace(en_punc, ar_punc)

        # Fix spacing around Arabic punctuation
        text = re.sub(r'\s+([،؟؛.])', r'\1', text)
        text = re.sub(r'([،؟؛.])(?=[^\s\n])', r'\1 ', text)

        return text

    def normalize_english(self, text: str) -> str:
        """Applies English specific capitalization and formatting."""
        if not text:
            return ""

        # Fix capitalization of isolated 'i' -> 'I'
        text = re.sub(r'\b(i)\b', 'I', text)

        # Fix spacing around standard punctuation
        text = re.sub(r'\s+([,.?!])', r'\1', text)
        text = re.sub(r'([,.?!])(?=[^\s\n])', r'\1 ', text)

        # Capitalize start of sentences
        sentences = re.split(r'([.?!]\s*)', text)
        capitalized_parts = []
        capitalize_next = True
        for part in sentences:
            if not part:
                continue
            if capitalize_next and part[0].isalpha():
                part = part[0].upper() + part[1:]
                capitalize_next = False
            if re.search(r'[.?!]', part):
                capitalize_next = True
            capitalized_parts.append(part)

        return "".join(capitalized_parts)

    def process_token(self, token: str, lang: str = 'auto', previous_text: str = "") -> Tuple[str, str]:
        """
        Full pipeline processing:
        1. Voice commands parsing ("فاصلة" -> "،", "comma" -> ",")
        2. Snippet expansion ("بريدي" -> "email@domain.com")
        3. Profanity filtering
        4. Language normalization (Arabic / English)
        5. Smart space handling
        """
        if not token:
            return "", lang

        active_lang = lang
        if active_lang == 'auto':
            active_lang = self.detect_dominant_language(token)

        # Step 1: Voice Commands
        text = self.voice_parser.parse(token, active_lang)

        # Step 2: Custom Snippets
        if self.enable_snippets:
            text = self.snippet_mgr.expand(text)

        # Step 3: Profanity Masking
        text = self.mask_profanity(text)

        # Step 4: Language-specific formatting
        if active_lang == 'ar':
            formatted = self.normalize_arabic(text)
        else:
            formatted = self.normalize_english(text)

        # Step 5: Leading Space Logic
        if previous_text and not previous_text.endswith((' ', '\n', '\t', '،', '؟', '.', ',', '!', '?')):
            if not formatted.startswith((' ', '\n', '،', '؟', '.', ',', '!', '?', ';', '؛')):
                formatted = ' ' + formatted

        return formatted, active_lang
