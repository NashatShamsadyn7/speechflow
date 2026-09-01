/**
 * SpeechFlow Web Pro Max Engine
 * Trilingual Localization (EN / AR / KU), Live Web Speech Simulation, Audio EQ Visualizer,
 * and Dynamic GitHub Releases 404 Prevention.
 * by Nashat Shamsadyn Ahmed
 */

// Trilingual Translations Dictionary
const translations = {
  en: {
    nav_sim: "Simulator",
    nav_speed: "0.04ms Speed",
    nav_features: "22 Features",
    nav_author: "Developer",
    nav_download: "Get SpeechFlow",
    hero_pill_badge: "Windows 11 Voice Typing • Ultra-Compact 1-Line Bar",
    hero_h1_1: "Voice to Text at",
    hero_h1_highlight: "< 0.05ms Speed.",
    hero_h1_2: "Flawless Arabic & English.",
    hero_desc: "A minimalist, single-line floating bar (Height: 36px) that types directly into any Windows app. Built with native Win32 Unicode injection — zero keyboard layout bugs, voice punctuation commands, AI polishing, and 100% free offline execution for 250,000+ users.",
    hero_dl_btn: "Download for Windows",
    hero_sim_btn: "Test Live Web Simulator",
    sim_badge: "Interactive Experience",
    sim_title: "The Ultra-Compact 1-Line Win+H Bar",
    sim_sub: "No bulky boxes. Just a sleek, 36px floating bar that types right into your cursor position. Try it live:"
  },
  ar: {
    nav_sim: "المحاكي المباشر",
    nav_speed: "سرعة 0.04ms",
    nav_features: "٢٢ ميزة",
    nav_author: "المطوّر",
    nav_download: "تحميل البرنامج",
    hero_pill_badge: "الكتابة الصوتية لويندوز 11 • شريط نحيف بسطر واحد",
    hero_h1_1: "تحويل الصوت إلى نص بسرعة",
    hero_h1_highlight: "أقل من 0.05 ميلي ثانية.",
    hero_h1_2: "دقة فائقة في العربية والإنجليزية.",
    hero_desc: "شريط عائم فائق النحافة بسطر واحد (ارتفاع 36px) يكتب مباشرة في أي تطبيق على ويندوز عبر Win32 Unicode — بدون أي مشاكل في لغة الكيبورد، مع علامات الترقيم الصوتية والتنظيف بالذكاء الاصطناعي وتشغيل محلي مجاني تماماً لـ ٢٥٠ ألف مستخدم.",
    hero_dl_btn: "تحميل لويندوز (مباشر)",
    hero_sim_btn: "جرب المحاكي في المتصفح",
    sim_badge: "تجربة تفاعلية حية",
    sim_title: "شريط Win+H فائق النحافة (سطر واحد)",
    sim_sub: "بدون أي نوافذ ضخمة أو تشويش. شريط أنيق بارتفاع 36px يكتب فوراً عند مكان المؤشر:"
  },
  ku: {
    nav_sim: "تاقیکردنەوە",
    nav_speed: "خێرایی 0.04ms",
    nav_features: "٢٢ تایبەتمەندی",
    nav_author: "گەشەپێدەر",
    nav_download: "داگرتنی بەرنامە",
    hero_pill_badge: "دەنگ بۆ نووسین لە ویندۆز 11 • شریتی یەک هێڵی باریک",
    hero_h1_1: "گۆڕینی دەنگ بۆ نووسین بە",
    hero_h1_highlight: "خێرایی کەمتر لە 0.05ms.",
    hero_h1_2: "بە کوردی، عەرەبی و ئینگلیزی.",
    hero_desc: "شریتێکی سەرکەوتووی باریک بە بەرزایی ٣٦ پێکسڵ کە ڕاستەوخۆ دەنووسێتە ناو هەموو بەرنامەکانی ویندۆز بێ کێشەی کیبۆرد، بە فرمانی دەنگی پۆینت و فاریزە و کارکردنی ئۆفلاین بۆ زیاتر لە ٢٥٠ هەزار بەکارهێنەر.",
    hero_dl_btn: "داگرتن بۆ ویندۆز",
    hero_sim_btn: "تاقیکردنەوە لە وێب",
    sim_badge: "تاقیکردنەوەی ڕاستەوخۆ",
    sim_title: "شریتی Win+H ی یەک هێڵی",
    sim_sub: "بێ هیچ پەنجەرەیەکی گەورە، ڕاستەوخۆ دەنووسێتە سەر شاشەکەت:"
  }
};

let currentSiteLang = 'en';
let isDictating = false;
let simLang = 'ar';
let recognition = null;

// Audio Visualizer Canvas Setup
const canvas = document.getElementById('miniEqCanvas');
const ctx = canvas ? canvas.getContext('2d') : null;
let eqBars = Array(8).fill(3);
let eqPhase = 0;

function animateMiniEq() {
  if (!ctx || !canvas) return;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  eqPhase += 0.2;
  const barW = 3.5;
  const gap = 3;

  for (let i = 0; i < 8; i++) {
    const target = isDictating ? Math.sin(eqPhase + i * 0.6) * 6 + 9 : 2.5;
    eqBars[i] += (target - eqBars[i]) * 0.35;
    const x = i * (barW + gap) + 4;
    const y = (canvas.height - eqBars[i]) / 2;

    const grad = ctx.createLinearGradient(x, y, x, y + eqBars[i]);
    if (isDictating) {
      grad.addColorStop(0, '#00ffa3');
      grad.addColorStop(1, '#00f0ff');
    } else {
      grad.addColorStop(0, '#475569');
      grad.addColorStop(1, '#334155');
    }

    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.roundRect(x, y, barW, eqBars[i], 1.5);
    ctx.fill();
  }
  requestAnimationFrame(animateMiniEq);
}
animateMiniEq();

// Site Language Dropdown Switcher
function toggleSiteLangMenu() {
  const menu = document.getElementById('siteLangMenu');
  if (menu) menu.classList.toggle('show');
}

document.addEventListener('click', (e) => {
  if (!e.target.closest('.lang-switch-dropdown')) {
    const menu = document.getElementById('siteLangMenu');
    if (menu) menu.classList.remove('show');
  }
});

function setSiteLanguage(lang) {
  currentSiteLang = lang;
  const root = document.getElementById('htmlRoot');
  const label = document.getElementById('currentSiteLangLabel');
  
  if (lang === 'ar') {
    root.dir = 'rtl';
    root.lang = 'ar';
    label.textContent = 'العربية';
  } else if (lang === 'ku') {
    root.dir = 'rtl';
    root.lang = 'ku';
    label.textContent = 'کوردی';
  } else {
    root.dir = 'ltr';
    root.lang = 'en';
    label.textContent = 'English';
  }

  const dict = translations[lang] || translations.en;
  document.querySelectorAll('[data-i18n]').forEach(el => {
    const key = el.getAttribute('data-i18n');
    if (dict[key]) el.textContent = dict[key];
  });

  const menu = document.getElementById('siteLangMenu');
  if (menu) menu.classList.remove('show');
}

// 1-Line Pill Controls
function toggleWebDictation() {
  if (!isDictating) {
    startBrowserMic();
  } else {
    stopBrowserMic();
  }
}

function cycleSimulatorLang() {
  const label = document.getElementById('pillLangLabel');
  const editor = document.getElementById('virtualEditor');
  if (simLang === 'ar') {
    simLang = 'en';
    label.textContent = 'EN';
    editor.dir = 'ltr';
  } else if (simLang === 'en') {
    simLang = 'ku';
    label.textContent = 'KU';
    editor.dir = 'rtl';
  } else {
    simLang = 'ar';
    label.textContent = 'AR';
    editor.dir = 'rtl';
  }
}

function startBrowserMic() {
  const SpeechRec = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (SpeechRec) {
    try {
      recognition = new SpeechRec();
      recognition.continuous = true;
      recognition.interimResults = true;
      recognition.lang = simLang === 'ar' ? 'ar-SA' : (simLang === 'ku' ? 'ar-IQ' : 'en-US');

      recognition.onstart = () => setPillState(true);
      recognition.onresult = (e) => {
        let finalText = '';
        for (let i = e.resultIndex; i < e.results.length; ++i) {
          if (e.results[i].isFinal) {
            finalText += e.results[i][0].transcript;
          }
        }
        if (finalText) {
          typeIntoEditor(finalText);
        }
      };
      recognition.onerror = () => setPillState(true);
      recognition.onend = () => { if (isDictating) setPillState(false); };
      recognition.start();
    } catch(err) {
      setPillState(true);
    }
  } else {
    setPillState(true);
  }
}

function stopBrowserMic() {
  if (recognition) {
    try { recognition.stop(); } catch(e){}
  }
  setPillState(false);
}

function setPillState(active) {
  isDictating = active;
  const dot = document.getElementById('pillDot');
  const mic = document.getElementById('pillMic');
  const icon = document.getElementById('pillMicIcon');

  if (active) {
    dot.classList.add('active');
    mic.classList.add('recording');
    icon.textContent = '⏹️';
  } else {
    dot.classList.remove('active');
    mic.classList.remove('recording');
    icon.textContent = '🎙️';
  }
}

// Voice Command Replacer & Fast Typing
const voiceCmds = [
  { p: /\b(فاصلة|فاصله)\b/gi, r: '،' },
  { p: /\b(نقطة|نقطه)\b/gi, r: '.' },
  { p: /\b(علامة استفهام|علامه استفهام)\b/gi, r: '؟' },
  { p: /\b(سطر جديد|انزل سطر)\b/gi, r: '\n' },
  { p: /\b(comma)\b/gi, r: ',' },
  { p: /\b(period|full stop)\b/gi, r: '.' },
  { p: /\b(question mark)\b/gi, r: '?' },
  { p: /\b(new line)\b/gi, r: '\n' },
  { p: /\b(بريدي الإلكتروني|بريدي)\b/gi, r: 'myemail@domain.com' }
];

function typeIntoEditor(raw) {
  let text = raw;
  voiceCmds.forEach(c => { text = text.replace(c.p, c.r); });

  const editor = document.getElementById('virtualEditor');
  let idx = 0;
  function step() {
    if (idx < text.length) {
      editor.value += text[idx];
      editor.scrollTop = editor.scrollHeight;
      idx++;
      setTimeout(step, 6);
    } else {
      editor.value += ' ';
    }
  }
  step();
}

function simulatePhrase(phrase, lang) {
  if (lang) {
    simLang = lang;
    document.getElementById('pillLangLabel').textContent = lang.toUpperCase();
    document.getElementById('virtualEditor').dir = lang === 'en' ? 'ltr' : 'rtl';
  }

  setPillState(true);
  setTimeout(() => {
    setPillState(false);
    typeIntoEditor(phrase);
  }, 350);
}

function clearVirtualEditor() {
  document.getElementById('virtualEditor').value = '';
}

function copyVirtualEditor() {
  const ed = document.getElementById('virtualEditor');
  navigator.clipboard.writeText(ed.value);
  const btn = document.getElementById('copyEditorBtn');
  btn.textContent = '✅ Copied!';
  setTimeout(() => { btn.textContent = '📋 Copy'; }, 1500);
}

function copyChecksum() {
  const code = document.getElementById('checksumCode').textContent;
  navigator.clipboard.writeText(code);
  alert('Checksum copied to clipboard:\n' + code);
}

function openSimSettings() {
  alert("SpeechFlow Settings\n• Default Language: Trilingual (AR/EN/KU)\n• Engine: Local faster-whisper (Offline) + Deepgram Nova-2\n• Hotkey: Ctrl + Shift + Space");
}

// Auto-resolve dynamic GitHub release to prevent 404
async function updateDownloadLink() {
  const dlBtn = document.getElementById('mainDownloadBtn');
  if (!dlBtn) return;
  try {
    const res = await fetch('https://api.github.com/repos/NashatShamsadyn7/speechflow/releases/latest');
    if (res.ok) {
      const data = await res.json();
      if (data.assets && data.assets.length > 0) {
        const exe = data.assets.find(a => a.name.endsWith('.exe')) || data.assets[0];
        dlBtn.href = exe.browser_download_url;
      }
    }
  } catch(e) {
    // Falls back to direct tag link
  }
}
document.addEventListener('DOMContentLoaded', updateDownloadLink);
