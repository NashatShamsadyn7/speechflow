"""
Dictation History Log Manager
Persistently records all voice transcriptions with timestamps and search/export capabilities.
"""

import os
import json
import time
from typing import List, Dict, Optional

HISTORY_FILE = os.path.join(os.path.dirname(__file__), "..", "history.json")

class HistoryManager:
    def __init__(self, file_path: str = HISTORY_FILE, max_entries: int = 500):
        self.file_path = file_path
        self.max_entries = max_entries
        self.entries: List[Dict] = []
        self.load()

    def load(self):
        if os.path.exists(self.file_path):
            try:
                with open(self.file_path, "r", encoding="utf-8") as f:
                    self.entries = json.load(f)
                return
            except Exception:
                pass
        self.entries = []

    def save(self):
        try:
            with open(self.file_path, "w", encoding="utf-8") as f:
                json.dump(self.entries, f, indent=2, ensure_ascii=False)
        except Exception as e:
            print(f"[HistoryManager] Save error: {e}")

    def add_entry(self, text: str, lang: str = "ar") -> Dict:
        """Adds a new transcription entry to history."""
        cleaned = text.strip()
        if not cleaned:
            return None

        entry = {
            "id": int(time.time() * 1000),
            "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
            "text": cleaned,
            "lang": lang,
            "word_count": len(cleaned.split()),
            "char_count": len(cleaned)
        }

        self.entries.insert(0, entry)
        if len(self.entries) > self.max_entries:
            self.entries = self.entries[:self.max_entries]

        self.save()
        return entry

    def get_history(self, search_query: str = "", limit: int = 100) -> List[Dict]:
        """Retrieves history entries with optional fuzzy search."""
        if not search_query:
            return self.entries[:limit]

        query = search_query.lower()
        results = [e for e in self.entries if query in e["text"].lower()]
        return results[:limit]

    def clear_history(self):
        self.entries = []
        self.save()

    def export_txt(self, export_path: str):
        with open(export_path, "w", encoding="utf-8") as f:
            for e in self.entries:
                f.write(f"[{e['timestamp']}] ({e['lang'].upper()}) - {e['text']}\n")
