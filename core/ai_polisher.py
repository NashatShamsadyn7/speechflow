"""
AI Text Polisher & Rewriter
Provides instant LLM-powered text enhancements: Grammar Fix, Professional Tone, Bullets, and Translation.
"""

import threading
from typing import Callable, Optional

try:
    from groq import Groq
except ImportError:
    Groq = None

AI_PROMPTS = {
    "grammar": "You are an expert Arabic and English editor. Fix all grammar, spelling, and punctuation errors in the provided text. Preserve the original meaning exactly. Output ONLY the polished text with no conversational preamble or quotes.",
    "professional": "Rewrite the following voice transcription into a highly professional, polite, and well-structured email / business message. Retain the core intent. Output ONLY the refined text.",
    "bullets": "Convert the following voice dictation into clear, concise, bullet points highlighting key action items. Output ONLY the bullet list.",
    "translate_ar_en": "If the text is Arabic, translate it to natural English. If it is English, translate it to natural Arabic. Output ONLY the translation.",
    "summarize": "Summarize the key message of the following dictation in 1-2 powerful sentences. Output ONLY the summary."
}

class AIPolisher:
    def __init__(self, api_key: str, model: str = "llama-3.3-70b-versatile"):
        self.api_key = api_key
        self.model = model
        self.client = Groq(api_key=self.api_key) if (Groq and self.api_key) else None

    def set_api_key(self, api_key: str):
        self.api_key = api_key
        self.client = Groq(api_key=self.api_key) if (Groq and self.api_key) else None

    def polish_text(self, text: str, mode: str = "grammar", custom_instruction: Optional[str] = None) -> str:
        """
        Synchronously transforms text using LLM.
        """
        if not text:
            return ""
        if not self.client:
            return text

        system_prompt = custom_instruction if custom_instruction else AI_PROMPTS.get(mode, AI_PROMPTS["grammar"])

        try:
            chat_completion = self.client.chat.completions.create(
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": text}
                ],
                model=self.model,
                temperature=0.2,
                max_tokens=1024
            )
            result = chat_completion.choices[0].message.content.strip()
            return result
        except Exception as e:
            print(f"[AIPolisher] Error: {e}")
            return text

    def polish_text_async(self, text: str, mode: str = "grammar", callback: Optional[Callable[[str], None]] = None):
        """Asynchronously transforms text in background thread."""
        def _run():
            res = self.polish_text(text, mode)
            if callback:
                callback(res)
        threading.Thread(target=_run, daemon=True).start()
