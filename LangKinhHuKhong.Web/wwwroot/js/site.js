// Browser-only transports: the UI is rendered by Blazor Server, not by JS.
window.lkh = (() => {
  let csrf = null;
  async function token() {
    if (csrf) return csrf;
    const res = await fetch('/api/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
    if (!res.ok) throw new Error('Không lấy được CSRF token.');
    csrf = (await res.json()).token;
    return csrf;
  }
  async function send(path, method = 'GET', body = null) {
    const mutation = !['GET', 'HEAD'].includes(method);
    const headers = { 'Accept': 'application/json' };
    if (mutation) headers['X-CSRF-TOKEN'] = await token();
    if (body !== null && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';
    const response = await fetch('/api' + path, {
      method,
      credentials: 'same-origin',
      cache: 'no-store',
      headers,
      body: mutation ? (body instanceof FormData ? body : body === null ? null : JSON.stringify(body)) : undefined
    });
    const type = response.headers.get('content-type') || '';
    const result = type.includes('json') ? await response.json() : {};
    if (!response.ok) {
      if (response.status === 400 && result.error?.includes('CSRF')) csrf = null;
      throw new Error(result.error || `Yêu cầu thất bại (HTTP ${response.status})`);
    }
    if (path === '/auth/login' || path === '/auth/logout') csrf = null;
    return result;
  }
  async function uploadEpisode(storyId, title, number) {
    const file = document.getElementById('episode-file')?.files?.[0];
    if (!file) throw new Error('Hãy chọn file MP3 trước khi upload.');
    if (!file.name.toLowerCase().endsWith('.mp3') || file.size > 250 * 1024 * 1024) throw new Error('Chỉ nhận MP3 tối đa 250MB.');
    const form = new FormData();
    form.append('title', title);
    form.append('episodeNumber', String(number));
    form.append('file', file, file.name);
    const result = await send(`/admin/stories/${encodeURIComponent(storyId)}/episodes`, 'POST', form);
    document.getElementById('episode-file').value = '';
    return result;
  }
  async function replaceEpisodeAudio(episodeId) {
    const input = document.getElementById('replace-episode-file');
    const file = input?.files?.[0];
    if (!file) throw new Error('Hãy chọn MP3 mới để thay thế.');
    if (!file.name.toLowerCase().endsWith('.mp3') || file.size > 250 * 1024 * 1024)
      throw new Error('Chỉ nhận MP3 tối đa 250MB.');
    const form = new FormData();
    form.append('file', file, file.name);
    const result = await send(`/admin/episodes/${encodeURIComponent(episodeId)}/audio`, 'PUT', form);
    input.value = '';
    return result;
  }
  async function uploadCover(storyId) {
    const file = document.getElementById('cover-file')?.files?.[0];
    if (!file) throw new Error('Hãy chọn ảnh bìa trước khi upload.');
    if (!/\.(png|jpe?g|webp)$/i.test(file.name) || file.size > 8 * 1024 * 1024)
      throw new Error('Chỉ nhận JPG, PNG, WebP tối đa 8MB.');
    const form = new FormData();
    form.append('file', file, file.name);
    const result = await send(`/admin/stories/${encodeURIComponent(storyId)}/cover`, 'POST', form);
    document.getElementById('cover-file').value = '';
    return result;
  }
  // Free -> direct 5-minute MP3 preview. Premium/Admin -> encrypted HLS only.
  // A browser cannot call the full MP3 route; the HLS playlist/segments/key are gated by server.
  let activeHls = null;
  let activeAudio = null;
  let hlsLoading = null;
  let sessionTimer = null;
  let listenerAbort = null;
  let playbackGeneration = 0;
  let autoNextReceiver = null;
  const playerPreferences = { speed: 1, volume: 1 };
  function bindAutoNext(receiver) { autoNextReceiver = receiver; }
  function stopCounting() {
    playbackGeneration++;
    if (sessionTimer !== null) { clearInterval(sessionTimer); sessionTimer = null; }
  }
  // Load hls.js only for Premium listeners on browsers without native HLS.
  // Free MP3 playback never waits for or downloads this library.
  function getHlsLibrary() {
    if (window.Hls) return Promise.resolve(window.Hls);
    if (!hlsLoading) hlsLoading = new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = 'https://cdn.jsdelivr.net/npm/hls.js@1.6.0/dist/hls.min.js';
      script.onload = () => window.Hls ? resolve(window.Hls) : reject(new Error('HLS.js unavailable'));
      script.onerror = () => reject(new Error('Không tải được hls.js'));
      document.head.appendChild(script);
    }).catch(error => { hlsLoading = null; throw error; });
    return hlsLoading;
  }
  function listen(episodeId) {
    const audio = document.getElementById('lkh-player');
    const root = document.getElementById('lkh-player-root');
    if (!audio || !root || (audio.dataset.boundId === episodeId && activeAudio === audio)) return;
    stopCounting();
    listenerAbort?.abort();
    listenerAbort = new AbortController();
    const on = (element, type, callback, opts = {}) => element?.addEventListener(type, callback, { ...opts, signal: listenerAbort.signal });
    if (activeHls) { activeHls.destroy(); activeHls = null; }
    if (activeAudio && activeAudio !== audio) { activeAudio.pause(); activeAudio.removeAttribute('src'); activeAudio.load(); }
    activeAudio = audio;
    audio.dataset.boundId = episodeId;
    const source = audio.dataset.lkhSrc || '';
    const premiumHls = audio.dataset.lkhMode === 'hls';
    const toggle = root.querySelector('#lkh-audio-toggle');
    const seek = root.querySelector('#lkh-audio-seek');
    const current = root.querySelector('#lkh-audio-current');
    const duration = root.querySelector('#lkh-audio-duration');
    const rateButton = root.querySelector('#lkh-audio-rate');
    const volume = root.querySelector('#lkh-audio-volume');
    const speeds = [0.75, 1, 1.25, 1.5, 1.75, 2];
    let lastSaved = -1;
    let dragging = false;
    let speedIndex = Math.max(0, speeds.indexOf(playerPreferences.speed));
    audio.playbackRate = speeds[speedIndex];
    audio.volume = playerPreferences.volume;
    if (rateButton) rateButton.textContent = `${speeds[speedIndex]}×`;
    if (volume) volume.value = String(Math.round(playerPreferences.volume * 100));
    async function beginCounting() {
      stopCounting();
      const generation = playbackGeneration;
      try {
        const result = await send(`/episodes/${encodeURIComponent(episodeId)}/playback/start`, 'POST');
        if (generation !== playbackGeneration || audio.paused || audio.dataset.boundId !== episodeId) return;
        const sessionId = result?.sessionId || '';
        if (!sessionId) return; // Admin is not counted toward popularity.
        sessionTimer = setInterval(() => {
          if (!audio.isConnected || audio.dataset.boundId !== episodeId || audio.paused || audio.ended ||
              audio.seeking || audio.readyState < 2) return;
          send(`/episodes/${encodeURIComponent(episodeId)}/playback/ping`, 'POST', { sessionId }).catch(() => {});
        }, 10000);
      } catch { /* A playback network failure must not interrupt the audio. */ }
    }

    function formatTime(seconds) {
      if (!Number.isFinite(seconds) || seconds < 0) return '0:00';
      const s = Math.floor(seconds);
      const h = Math.floor(s / 3600);
      const m = Math.floor((s % 3600) / 60);
      const ss = String(s % 60).padStart(2, '0');
      return h ? `${h}:${String(m).padStart(2, '0')}:${ss}` : `${m}:${ss}`;
    }
    function sync() {
      const total = Number.isFinite(audio.duration) ? audio.duration : 0;
      const now = Number.isFinite(audio.currentTime) ? audio.currentTime : 0;
      root.classList.toggle('is-playing', !audio.paused && !audio.ended);
      toggle?.setAttribute('aria-label', audio.paused ? 'Phát audio' : 'Tạm dừng audio');
      if (current) current.textContent = formatTime(now);
      if (duration) duration.textContent = total > 0 ? formatTime(total) : '--:--';
      if (seek && !dragging) seek.value = String(total > 0 ? Math.min(1000, Math.round(now / total * 1000)) : 0);
      if (seek) seek.style.setProperty('--player-progress', `${seek.value / 10}%`);
    }
    function save(force = false) {
      if (!audio.isConnected || audio.dataset.boundId !== episodeId) return;
      const now = Math.max(0, Math.floor(audio.currentTime || 0));
      if (!force && Math.abs(now - lastSaved) < 15) return;
      if (now === lastSaved) return;
      lastSaved = now;
      send(`/episodes/${encodeURIComponent(episodeId)}/progress`, 'PUT', { seconds: now }).catch(() => {});
    }

    on(toggle,'click', () => {
      if (audio.paused || audio.ended) audio.play().catch(() => sync());
      else audio.pause();
    });
    root.querySelectorAll('[data-lkh-skip]').forEach(button => {
      on(button,'click', () => {
        const offset = Number(button.dataset.lkhSkip || 0);
        const total = Number.isFinite(audio.duration) ? audio.duration : 0;
        if (!total) return;
        audio.currentTime = Math.min(total, Math.max(0, audio.currentTime + offset));
        sync();
        save(true);
      });
    });
    on(seek,'input', () => {
      dragging = true;
      const total = Number.isFinite(audio.duration) ? audio.duration : 0;
      if (current && total) current.textContent = formatTime(total * Number(seek.value) / 1000);
      seek.style.setProperty('--player-progress', `${Number(seek.value) / 10}%`);
    });
    on(seek,'change', () => {
      const total = Number.isFinite(audio.duration) ? audio.duration : 0;
      if (total) audio.currentTime = Math.min(total, total * Number(seek.value) / 1000);
      dragging = false;
      sync();
      save(true);
    });
    on(rateButton,'click', () => {
      speedIndex = (speedIndex + 1) % speeds.length;
      playerPreferences.speed = speeds[speedIndex];
      audio.playbackRate = playerPreferences.speed;
      rateButton.textContent = `${playerPreferences.speed}×`;
    });
    on(volume,'input', () => {
      playerPreferences.volume = Math.min(1, Math.max(0, Number(volume.value) / 100));
      audio.volume = playerPreferences.volume;
    });
    on(audio,'loadedmetadata', sync);
    on(audio,'durationchange', sync);
    on(audio,'timeupdate', () => { sync(); save(); });
    on(audio,'play', sync);
    on(audio,'playing', () => { sync(); beginCounting(); });
    on(audio,'waiting', stopCounting);
    on(audio,'pause', () => { stopCounting(); sync(); save(true); });
    on(audio,'ended', () => {
      stopCounting(); sync(); save(true);
      // Ask Blazor to move its playlist forward. Only the server decides which episode
      // is next; playback still uses the existing authenticated MP3/HLS endpoints.
      if (autoNextReceiver && audio.isConnected && activeAudio === audio &&
          audio.dataset.boundId === episodeId) {
        autoNextReceiver.invokeMethodAsync('OnAudioEnded', episodeId).catch(error => {
          console.warn('[LKH] Không thể chuyển tập tự động:', error);
        });
      }
    });
    on(audio,'error', () => {
      if (current) current.textContent = 'Lỗi phát';
      sync();
    });
    function startAudio() {
      if (activeAudio !== audio || !audio.isConnected || audio.dataset.boundId !== episodeId) return;
      audio.play().catch(() => {
        // Some mobile browsers require another tap after a media source change.
        // Leave the new episode visible with its Play button instead of retrying.
        sync();
      });
    }
    if (premiumHls) {
      if (audio.canPlayType('application/vnd.apple.mpegurl')) {
        audio.src = source; // Safari/iOS uses native HLS.
        on(audio,'loadedmetadata', startAudio, { once: true });
      } else {
        getHlsLibrary().then(Hls => {
          if (!audio.isConnected || activeAudio !== audio || audio.dataset.boundId !== episodeId) return;
          if (!Hls.isSupported()) {
            if (current) current.textContent = 'Trình duyệt không hỗ trợ HLS';
            return;
          }
          activeHls = new Hls({ maxBufferLength: 30, backBufferLength: 20,
            enableWorker: true, lowLatencyMode: false });
          activeHls.on(Hls.Events.ERROR, (_, info) => {
            if (info?.fatal && current) current.textContent = 'Không tải được HLS';
          });
          activeHls.loadSource(source);
          activeHls.attachMedia(audio);
          activeHls.on(Hls.Events.MANIFEST_PARSED, startAudio);
        }).catch(() => { if (current) current.textContent = 'Không tải được thư viện HLS'; });
      }
    } else {
      audio.src = source; // Free MP3: no encryption or HLS overhead.
      startAudio();
    }
    sync();
  }
  function stopAudio() {
    stopCounting(); listenerAbort?.abort(); listenerAbort = null;
    if (activeHls) { activeHls.destroy(); activeHls = null; }
    if (activeAudio) { activeAudio.pause(); activeAudio.removeAttribute('src'); activeAudio.load(); activeAudio = null; }
  }
  const reconnectModal = document.getElementById('components-reconnect-modal');
  function isPlayingAudio() {
    return Boolean(activeAudio && !activeAudio.paused && !activeAudio.ended);
  }
  function silentReconnect() {
    if (!reconnectModal) return;
    if (reconnectModal.classList.contains('components-reconnect-failed')) {
      window.Blazor?.reconnect?.();
    } else if (reconnectModal.classList.contains('components-reconnect-rejected') && !isPlayingAudio()) {
      window.location.reload();
    }
  }
  reconnectModal?.addEventListener('components-reconnect-state-changed', event => {
    const state = event?.detail?.state;
    if (state === 'failed') {
      setTimeout(() => window.Blazor?.reconnect?.(), 1500);
    } else if (state === 'rejected' && !isPlayingAudio() && document.visibilityState === 'visible') {
      window.location.reload();
    }
  });
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') silentReconnect();
  });
  return { api: send, uploadEpisode, uploadCover, replaceEpisodeAudio, listen, stopAudio, bindAutoNext };
})();
window.lkh.reload = () => window.location.reload();
