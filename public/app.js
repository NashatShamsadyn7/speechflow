/**
 * SpeechFlow Web Interactive Simulator
 * Supports Web Speech API live microphone testing, sample phrases, voice punctuation, and animated EQ canvas.
 * by Nashat Shamsadyn Ahmed
 */

let isSimRecording = false;
let currentSimLang = 'ar';
let recognition = null;

const virtualEditor = document.getElementById('virtualEditor');
const demoTicker = document.getElementById('demoTicker');
const simMicBtn = document.getElementById('simMicBtn');
const simLangBtn = document.getElementById('simLangBtn');
const barStatusDot = document.getElementById('barStatusDot');
const langText = document.getElementById('langText');
const micText = document.getElementById('micText');
const micIcon = document.getElementById('micIcon');
const canvas = document.getElementById('audioCanvas');
const ctx = canvas.getContext('2d');

// Dynamic GitHub Release 404-Prevention Resolver
async function resolveLatestRelease() {
  const downloadBtn = document.getElementById('downloadBtn');
  if (!downloadBtn) return;
  try {
    const res = await fetch('https://api.github.com/repos/NashatShamsadyn7/speechflow/releases/latest');
    if (res.ok) {
      const data = await res.json();
      if (data.assets && data.assets.length > 0) {
        const exeAsset = data.assets.find(a => a.name.endsWith('.exe')) || data.assets[0];
        downloadBtn.href = exeAsset.browser_download_url;
      }
    }
  } catch (err) {
    console.log('Using default release fallback URL');
  }
}
document.addEventListener('DOMContentLoaded', resolveLatestRelease);

// Voice Command Map
const voiceCommandMap = [
  { pattern: /\b(فاصلة|فاصله)\b/gi, rep: '،' },
  { pattern: /\b(نقطة|نقطه)\b/gi, rep: '.' },
  { pattern: /\b(علامة استفهام|علامه استفهام)\b/gi, rep: '؟' },
  { pattern: /\b(سطر جديد|انزل سطر)\b/gi, rep: '\n' },
  { pattern: /\b(comma)\b/gi, rep: ',' },
  { pattern: /\b(period|dot)\b/gi, rep: '.' },
  { pattern: /\b(question mark)\b/gi, rep: '?' },
  { pattern: /\b(new line)\b/gi, rep: '\n' },
  { pattern: /\b(بريدي الإلكتروني|بريدي)\b/gi, rep: 'myemail@domain.com' }
];

// Audio Waveform Animation
let audioBars = Array(10).fill(2);
let wavePhase = 0;

function drawAudioWaveform() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  wavePhase += 0.15;
  const barWidth = 4;
  const spacing = 3;

  for (let i = 0; i < 10; i++) {
    const targetH = isSimRecording
      ? Math.sin(wavePhase + i * 0.5) * 8 + 12
      : 3;
    audioBars[i] += (targetH - audioBars[i]) * 0.3;

    const x = i * (barWidth + spacing) + 5;
    const y = (canvas.height - audioBars[i]) / 2;

    const grad = ctx.createLinearGradient(x, y, x, y + audioBars[i]);
    if (isSimRecording) {
      grad.addColorStop(0, '#00ffa3');
      grad.addColorStop(1, '#00f0ff');
    } else {
      grad.addColorStop(0, '#475569');
      grad.addColorStop(1, '#334155');
    }

    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.roundRect(x, y, barWidth, audioBars[i], 2);
    ctx.fill();
  }
  requestAnimationFrame(drawAudioWaveform);
}
drawAudioWaveform();

// Toggle Language Button
simLangBtn.addEventListener('click', () => {
  if (currentSimLang === 'ar') {
    currentSimLang = 'en';
    langText.textContent = 'English (EN)';
    virtualEditor.dir = 'ltr';
  } else if (currentSimLang === 'en') {
    currentSimLang = 'ku';
    langText.textContent = 'Kurdish (KU)';
    virtualEditor.dir = 'rtl';
  } else {
    currentSimLang = 'ar';
    langText.textContent = 'العربية (AR)';
    virtualEditor.dir = 'rtl';
  }
});

// Toggle Mic Button
simMicBtn.addEventListener('click', () => {
  if (!isSimRecording) {
    startBrowserSpeech();
  } else {
    stopBrowserSpeech();
  }
});

function startBrowserSpeech() {
  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (SpeechRecognition) {
    try {
      recognition = new SpeechRecognition();
      recognition.continuous = true;
      recognition.interimResults = true;
      recognition.lang = currentSimLang === 'ar' ? 'ar-SA' : (currentSimLang === 'ku' ? 'ar-IQ' : 'en-US');

      recognition.onstart = () => setRecordingUI(true);
      recognition.onresult = (event) => {
        let interimText = '';
        let finalText = '';

        for (let i = event.resultIndex; i < event.results.length; ++i) {
          if (event.results[i].isFinal) {
            finalText += event.results[i][0].transcript;
          } else {
            interimText += event.results[i][0].transcript;
          }
        }

        if (interimText) {
          demoTicker.innerHTML = `<i>... ${interimText}</i>`;
        }
        if (finalText) {
          typeProcessedText(finalText);
        }
      };

      recognition.onerror = (e) => {
        demoTicker.textContent = `Mic active (Testing simulator)`;
      };

      recognition.onend = () => {
        if (isSimRecording) setRecordingUI(false);
      };

      recognition.start();
    } catch (e) {
      setRecordingUI(true);
    }
  } else {
    // Fallback simulated dictation
    setRecordingUI(true);
    demoTicker.textContent = 'Speak or click a sample phrase below...';
  }
}

function stopBrowserSpeech() {
  if (recognition) {
    try { recognition.stop(); } catch(e){}
  }
  setRecordingUI(false);
}

function setRecordingUI(recording) {
  isSimRecording = recording;
  if (recording) {
    simMicBtn.classList.add('recording');
    barStatusDot.classList.add('active');
    micIcon.textContent = '⏹️';
    micText.textContent = 'Stop';
    demoTicker.innerHTML = '<i>Listening... Speak now</i>';
  } else {
    simMicBtn.classList.remove('recording');
    barStatusDot.classList.remove('active');
    micIcon.textContent = '🎙️';
    micText.textContent = 'Start';
    demoTicker.textContent = 'Ready • Click Start or a phrase below';
  }
}

// Process voice commands and type into editor
function typeProcessedText(text) {
  let processed = text;
  voiceCommandMap.forEach(cmd => {
    processed = processed.replace(cmd.pattern, cmd.rep);
  });

  demoTicker.textContent = processed;
  
  // Animate live typing
  let index = 0;
  function typeNext() {
    if (index < processed.length) {
      virtualEditor.value += processed[index];
      virtualEditor.scrollTop = virtualEditor.scrollHeight;
      index++;
      setTimeout(typeNext, 8);
    } else {
      virtualEditor.value += ' ';
    }
  }
  typeNext();
}

// Simulate sample phrases
function simulateVoice(phrase, lang) {
  if (lang) {
    currentSimLang = lang;
    langText.textContent = lang === 'ar' ? 'العربية (AR)' : (lang === 'ku' ? 'Kurdish (KU)' : 'English (EN)');
    virtualEditor.dir = lang === 'en' ? 'ltr' : 'rtl';
  }

  setRecordingUI(true);
  demoTicker.innerHTML = `<i>... ${phrase}</i>`;

  setTimeout(() => {
    setRecordingUI(false);
    typeProcessedText(phrase);
  }, 400);
}

function copyEditorText() {
  navigator.clipboard.writeText(virtualEditor.value);
  const btn = document.getElementById('copyEditorBtn');
  btn.textContent = '✅ Copied!';
  setTimeout(() => { btn.textContent = '📋 Copy Text'; }, 1500);
}
