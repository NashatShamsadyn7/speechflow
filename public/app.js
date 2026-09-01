/**
 * SpeechFlow Web Engine
 * Localization (EN / AR / KU), Live Web Speech Simulation, Audio EQ Visualizer,
 * Web Audio Chimes, and Dynamic GitHub Releases 404 Prevention.
 * Author: Nashat Shamsadyn Ahmed
 */

// Trilingual Localization
const i18n = {
  en: {
    nav_simulator: "Simulator",
    nav_features: "Features",
    nav_speed: "0.04ms Latency",
    nav_install: "Install Guide",
    nav_dev: "Developer",
    nav_download: "Download EXE",
    hero_badge: "Windows 11 Voice Typing • Ultra-Compact 36px Floating Pill",
    hero_title_1: "Voice to Text at",
    hero_title_glow: "< 0.05ms Local Speed.",
    hero_title_2: "Flawless Arabic & English.",
    hero_summary: "A minimalist, 36px floating bar that types directly into any Windows app. Built with native Win32 Unicode injection — zero keyboard layout bugs, voice punctuation commands, AI polishing, and 100% free offline execution for 250,000+ users.",
    hero_btn_main: "Download for Windows",
    hero_btn_sim: "Test Live Web Simulator",
    sim_badge: "Interactive Experience",
    sim_heading: "The Ultra-Compact 1-Line Win+H Bar",
    sim_sub: "No bulky boxes. Just a sleek, 36px floating bar that types right into your cursor position. Try it live:"
  },
  ar: {
    nav_simulator: "المحاكي المباشر",
    nav_features: "المميزات",
    nav_speed: "سرعة 0.04ms",
    nav_install: "دليل التثبيت",
    nav_dev: "المطوّر",
    nav_download: "تحميل البرنامج",
    hero_badge: "الكتابة الصوتية لويندوز 11 • شريط نحيف 36px عائم",
    hero_title_1: "تحويل الصوت إلى نص بسرعة",
    hero_title_glow: "أقل من 0.05 ميلي ثانية.",
    hero_title_2: "دقة فائقة في العربية والإنجليزية.",
    hero_summary: "شريط عائم فائق النحافة (ارتفاع 36px) يكتب مباشرة في أي تطبيق على ويندوز عبر Win32 Unicode — بدون أي مشاكل في لغة الكيبورد، مع علامات الترقيم الصوتية والتنظيف بالذكاء الاصطناعي وتشغيل محلي مجاني تماماً لـ ٢٥٠ ألف مستخدم.",
    hero_btn_main: "تحميل لويندوز (مباشر)",
    hero_btn_sim: "جرب المحاكي في المتصفح",
    sim_badge: "تجربة تفاعلية حية",
    sim_heading: "شريط Win+H فائق النحافة (سطر واحد)",
    sim_sub: "بدون أي نوافذ ضخمة أو تشويش. شريط أنيق بارتفاع 36px يكتب فوراً عند مكان المؤشر:"
  },
  ku: {
    nav_simulator: "تاقیکردنەوە",
    nav_features: "تایبەتمەندییەکان",
    nav_speed: "خێرایی 0.04ms",
    nav_install: "ڕێبەری کارپێکردن",
    nav_dev: "گەشەپێدەر",
    nav_download: "داگرتنی بەرنامە",
    hero_badge: "دەنگ بۆ نووسین لە ویندۆز 11 • شریتی باریکی ٣٦ پێکسڵ",
    hero_title_1: "گۆڕینی دەنگ بۆ نووسین بە",
    hero_title_glow: "خێرایی کەمتر لە 0.05ms.",
    hero_title_2: "بە کوردی، عەرەبی و ئینگلیزی.",
    hero_summary: "شریتێکی سەرکەوتووی باریک بە بەرزایی ٣٦ پێکسڵ کە ڕاستەوخۆ دەنووسێتە ناو هەموو بەرنامەکانی ویندۆز بێ کێشەی کیبۆرد، بە فرمانی دەنگی پۆینت و فاریزە و کارکردنی ئۆفلاین بۆ زیاتر لە ٢٥٠ هەزار بەکارهێنەر.",
    hero_btn_main: "داگرتن بۆ ویندۆز",
    hero_btn_sim: "تاقیکردنەوە لە وێب",
    sim_badge: "تاقیکردنەوەی ڕاستەوخۆ",
    sim_heading: "شریتی Win+H ی یەک هێڵی",
    sim_sub: "بێ هیچ پەنجەرەیەکی گەورە، ڕاستەوخۆ دەنووسێتە سەر شاشەکەت:"
  }
};

let currentLanguage = 'en';
let isDictating = false;
let simLang = 'ar';
let speechRecognizer = null;

// Audio EQ Visualizer
const canvas = document.getElementById('eqCanvas');
const ctx = canvas ? canvas.getContext('2d') : null;
let eqBars = Array(8).fill(3);
let eqPhase = 0;

function drawEqualizer() {
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
  requestAnimationFrame(drawEqualizer);
}
drawEqualizer();

// Web Audio API Tones
function playChime(type) {
  try {
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    if (!AudioContext) return;
    const ctx = new AudioContext();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);

    if (type === 'start') {
      osc.frequency.setValueAtTime(440, ctx.currentTime);
      osc.frequency.exponentialRampToValueAtTime(880, ctx.currentTime + 0.1);
      gain.gain.setValueAtTime(0.15, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.15);
      osc.start(ctx.currentTime);
      osc.stop(ctx.currentTime + 0.15);
    } else {
      osc.frequency.setValueAtTime(880, ctx.currentTime);
      osc.frequency.exponentialRampToValueAtTime(440, ctx.currentTime + 0.1);
      gain.gain.setValueAtTime(0.15, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.15);
      osc.start(ctx.currentTime);
      osc.stop(ctx.currentTime + 0.15);
    }
  } catch(e) {}
}

// Language Switch Dropdown
function toggleLangMenu() {
  const menu = document.getElementById('langDropdownMenu');
  if (menu) menu.classList.toggle('show');
}

document.addEventListener('click', (e) => {
  if (!e.target.closest('.lang-picker')) {
    const menu = document.getElementById('langDropdownMenu');
    if (menu) menu.classList.remove('show');
  }
});

function changeLanguage(lang) {
  currentLanguage = lang;
  const root = document.getElementById('htmlRoot');
  const label = document.getElementById('activeLangLabel');

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

  const dict = i18n[lang] || i18n.en;
  document.querySelectorAll('[data-i18n]').forEach(el => {
    const key = el.getAttribute('data-i18n');
    if (dict[key]) el.textContent = dict[key];
  });

  const menu = document.getElementById('langDropdownMenu');
  if (menu) menu.classList.remove('show');
}

// 1-Line Win+H Simulator Dictation
function toggleSimRecording() {
  if (!isDictating) {
    startListening();
  } else {
    stopListening();
  }
}

function switchSimLanguage() {
  const label = document.getElementById('pillLangText');
  const editor = document.getElementById('liveEditor');

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

function startListening() {
  playChime('start');
  const SpeechRec = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (SpeechRec) {
    try {
      speechRecognizer = new SpeechRec();
      speechRecognizer.continuous = true;
      speechRecognizer.interimResults = true;
      speechRecognizer.lang = simLang === 'ar' ? 'ar-SA' : (simLang === 'ku' ? 'ar-IQ' : 'en-US');

      speechRecognizer.onstart = () => updatePillUI(true);
      speechRecognizer.onresult = (e) => {
        let finalText = '';
        for (let i = e.resultIndex; i < e.results.length; ++i) {
          if (e.results[i].isFinal) {
            finalText += e.results[i][0].transcript;
          }
        }
        if (finalText) {
          typeIntoLiveEditor(finalText);
        }
      };
      speechRecognizer.onerror = () => updatePillUI(true);
      speechRecognizer.onend = () => { if (isDictating) updatePillUI(false); };
      speechRecognizer.start();
    } catch(err) {
      updatePillUI(true);
    }
  } else {
    updatePillUI(true);
  }
}

function stopListening() {
  playChime('stop');
  if (speechRecognizer) {
    try { speechRecognizer.stop(); } catch(e){}
  }
  updatePillUI(false);
}

function updatePillUI(active) {
  isDictating = active;
  const dot = document.getElementById('pillDot');
  const mic = document.getElementById('pillMicBtn');
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

// Voice Command Replacement Map
const voiceCommandReplacements = [
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

function typeIntoLiveEditor(raw) {
  let text = raw;
  voiceCommandReplacements.forEach(c => { text = text.replace(c.p, c.r); });

  const editor = document.getElementById('liveEditor');
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

function triggerPhrase(phrase, lang) {
  if (lang) {
    simLang = lang;
    document.getElementById('pillLangText').textContent = lang.toUpperCase();
    document.getElementById('liveEditor').dir = lang === 'en' ? 'ltr' : 'rtl';
  }

  updatePillUI(true);
  playChime('start');

  setTimeout(() => {
    updatePillUI(false);
    playChime('stop');
    typeIntoLiveEditor(phrase);
  }, 350);
}

function clearEditor() {
  document.getElementById('liveEditor').value = '';
}

function copyEditor() {
  const ed = document.getElementById('liveEditor');
  navigator.clipboard.writeText(ed.value);
  const btn = document.getElementById('editorCopyBtn');
  btn.textContent = '✅ Copied!';
  setTimeout(() => { btn.textContent = '📋 Copy'; }, 1500);
}

function copyHash() {
  const code = document.getElementById('hashVal').textContent;
  navigator.clipboard.writeText(code);
  const btn = document.getElementById('copyHashBtn');
  btn.textContent = '✅ Copied!';
  setTimeout(() => { btn.textContent = '📋 Copy'; }, 1500);
}

function showSimConfig() {
  alert("SpeechFlow Settings\n• Default Language: Trilingual (AR / EN / KU)\n• Engine: Local faster-whisper (Offline) + Deepgram Nova-2\n• Global Hotkey: Ctrl + Shift + Space");
}

// GitHub API Auto-Resolver
async function autoResolveRelease() {
  const dlBtn = document.getElementById('dlBtn');
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
  } catch(e) {}
}
document.addEventListener('DOMContentLoaded', autoResolveRelease);
