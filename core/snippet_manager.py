"""
Custom Vocabulary & Voice Snippet Expansion Manager
Replaces spoken shorthand / trigger phrases with custom expanded text templates.
"""

import os
import json
import re
from typing import Dict, List, Tuple

SNIPPETS_FILE = os.path.join(os.path.dirname(__file__), "..", "snippets.json")

DEFAULT_SNIPPETS = {
    "بريدي الإلكتروني": "myemail@domain.com",
    "تحياتي": "مع خالص التحيات والتقدير،\nفيصل",
    "بسم الله": "بسم الله الرحمن الرحيم",
    "السلام عليكم": "السلام عليكم ورحمة الله وبركاته",
    "my email": "myemail@domain.com",
    "best regards": "Best regards,\nFaisal",
    "thank you": "Thank you very much for your time and assistance."
}

class SnippetManager:
    def __init__(self, file_path: str = SNIPPETS_FILE):
        self.file_path = file_path
        self.snippets: Dict[str, str] = {}
        self.load()

    def load(self):
        if os.path.exists(self.file_path):
            try:
                with open(self.file_path, "r", encoding="utf-8") as f:
                    self.snippets = json.load(f)
                return
            except Exception:
                pass
        self.snippets = dict(DEFAULT_SNIPPETS)
        self.save()

    def save(self):
        try:
            with open(self.file_path, "w", encoding="utf-8") as f:
                json.dump(self.snippets, f, indent=2, ensure_ascii=False)
        except Exception as e:
            print(f"[SnippetManager] Save error: {e}")

    def add_snippet(self, trigger: str, expansion: str):
        trigger = trigger.strip()
        if trigger and expansion:
            self.snippets[trigger] = expansion
            self.save()

    def remove_snippet(self, trigger: str):
        if trigger in self.snippets:
            del self.snippets[trigger]
            self.save()

    def get_all_snippets(self) -> Dict[str, str]:
        return dict(self.snippets)

    def expand(self, text: str) -> str:
        """Expands any registered snippet triggers in the text."""
        if not text:
            return text

        result = text
        for trigger, expansion in sorted(self.snippets.items(), key=lambda x: len(x[0]), reverse=True):
            # Case-insensitive word boundary match
            pattern = re.compile(re.escape(trigger), re.IGNORECASE)
            result = pattern.sub(expansion, result)

        return result
