// After saving a source, show the Sources list. A same-page "#sources" link would only scroll, so reload in that case.
window.mamnShowSources = url => {
  // A fresh "saved" marker makes this a real page load (so the new source is listed) that still lands on #sources.
  const target = new URL(url, location.href); target.searchParams.set('saved', String(Date.now())); target.hash = 'sources';
  location.assign(target.href);
};

// Upload a source with visible progress beside the button that started it. Resolves with the final page URL;
// rejects with the server's own error message (or a plain one) so it can be shown where the person is looking.
window.mamnUpload = (url, data, button, what) => new Promise((resolve, reject) => {
  let box = button.nextElementSibling;
  if (!box?.classList.contains('upload-progress')) {
    box = document.createElement('div'); box.className = 'upload-progress';
    box.innerHTML = '<div class="upload-bar" role="progressbar" aria-label="Upload progress" aria-valuemin="0" aria-valuemax="100"><span></span></div><p class="upload-progress-text" role="status" aria-live="polite"></p>';
    button.after(box);
  }
  const bar = box.querySelector('.upload-bar'), fill = bar.querySelector('span'), text = box.querySelector('.upload-progress-text');
  const size = (n, total) => total < 1024 * 1024 ? `${Math.round(n / 1024)} KB` : `${(n / 1024 / 1024).toFixed(1)} MiB`;
  const show = (pct, message, waiting) => {
    box.hidden = false; box.classList.remove('is-error'); bar.classList.toggle('is-waiting', !!waiting);
    fill.style.width = `${pct}%`; bar.setAttribute('aria-valuenow', String(pct)); text.textContent = message;
  };
  const fail = message => { box.classList.add('is-error'); bar.classList.remove('is-waiting'); text.textContent = message; reject(new Error(message)); };
  const original = button.dataset.label || button.textContent; button.dataset.label = original;
  button.disabled = true; button.textContent = 'Saving…';
  const restore = () => { button.disabled = false; button.textContent = original; };
  show(0, `Starting to upload your ${what}…`);
  const xhr = new XMLHttpRequest();
  xhr.open('POST', url); xhr.withCredentials = true;
  xhr.upload.addEventListener('progress', e => {
    if (!e.lengthComputable) return;
    const pct = Math.min(99, Math.round(100 * e.loaded / e.total));
    show(pct, `Uploading… ${size(e.loaded, e.total)} of ${size(e.total, e.total)} (${pct}%)`);
  });
  xhr.upload.addEventListener('load', () => show(100, `Uploaded. Checking and saving your ${what} privately — this can take a few seconds…`, true));
  xhr.addEventListener('load', () => {
    const page = new DOMParser().parseFromString(xhr.responseText || '', 'text/html');
    const error = page.querySelector('.notice.error')?.textContent?.trim();
    if (xhr.status >= 400 || error) { restore(); fail(error || `The ${what} could not be saved (HTTP ${xhr.status}). Please try again.`); return; }
    show(100, 'Saved. Opening your sources…'); resolve({ url: xhr.responseURL || url });
  });
  xhr.addEventListener('error', () => { restore(); fail(`The connection dropped while uploading your ${what}. Nothing was saved — please try again.`); });
  xhr.addEventListener('timeout', () => { restore(); fail(`The upload took too long. Nothing was saved — try again on a stronger connection.`); });
  xhr.timeout = 10 * 60 * 1000;
  xhr.send(data);
});

(() => {
  const start = document.querySelector('#start-recording');
  const stop = document.querySelector('#stop-recording');
  const save = document.querySelector('#save-recording');
  const status = document.querySelector('#recording-status');
  const preview = document.querySelector('#recording-preview');
  const review = document.querySelector('#recording-review');
  const uploadForm = document.querySelector('#source-upload-form');
  const attribution = document.querySelector('#Attribution');
  const permission = document.querySelector('#RecordingPermissionConfirmed');
  const recordingName = document.querySelector('#RecordingDisplayName');
  const indicator = document.querySelector('#recording-indicator');
  const state = document.querySelector('#recording-state');
  const time = document.querySelector('#recording-time');
  if (!start || !stop || !save || !status || !preview || !review || !uploadForm || !permission || !recordingName || !indicator || !state || !time) return;

  if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) {
    start.disabled = true;
    status.textContent = 'This browser can’t record here. Use “Upload a file” instead.';
    return;
  }

  let recorder;
  let stream;
  let recordingBlob;
  let previewUrl;
  let maximumDurationTimer;
  let elapsedTimer;
  let recordingStartedAt;
  let chunks = [];
  let hasUnsavedRecording = false;

  const setRecordingState = isRecording => {
    start.disabled = isRecording;
    stop.disabled = !isRecording;
    indicator.classList.toggle('is-recording', isRecording);
    start.classList.toggle('is-recording', isRecording);
    state.textContent = isRecording ? 'Microphone is recording' : 'Ready to record';
    const label = start.querySelector('span') || start;
    label.textContent = isRecording ? 'Recording' : 'Start';
    start.setAttribute('aria-label', isRecording ? 'Recording in progress' : 'Start recording');
  };

  const updateElapsed = () => {
    const seconds = Math.floor((Date.now() - recordingStartedAt) / 1000);
    time.textContent = `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
  };

  const stopElapsed = () => { clearInterval(elapsedTimer); elapsedTimer = undefined; };

  const stopTracks = () => {
    stream?.getTracks().forEach(track => track.stop());
    stream = undefined;
  };

  start.addEventListener('click', async () => {
    try {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
      review.hidden = true;
      recordingBlob = undefined;
      hasUnsavedRecording = false;
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      recorder = new MediaRecorder(stream);
      chunks = [];
      recorder.addEventListener('dataavailable', event => { if (event.data.size) chunks.push(event.data); });
      recorder.addEventListener('stop', () => {
        clearTimeout(maximumDurationTimer);
        stopElapsed();
        const contentType = recorder.mimeType || 'audio/webm';
        recordingBlob = new Blob(chunks, { type: contentType });
        previewUrl = URL.createObjectURL(recordingBlob);
        preview.src = previewUrl;
        review.hidden = false;
        hasUnsavedRecording = true;
        state.textContent = 'Recording ready to review';
        status.textContent = 'Listen back, name it, confirm permission, then save it to your sources.';
        stopTracks();
      });
      recorder.start();
      setRecordingState(true);
      recordingStartedAt = Date.now();
      time.textContent = '00:00';
      elapsedTimer = window.setInterval(updateElapsed, 1000);
      status.textContent = 'Recording… Stop when you are ready to review. The maximum recording length is five minutes.';
      maximumDurationTimer = window.setTimeout(() => {
        if (recorder?.state === 'recording') recorder.stop();
        setRecordingState(false);
      }, 5 * 60 * 1000);
    } catch {
      stopElapsed();
      stopTracks();
      setRecordingState(false);
      status.textContent = 'The microphone isn’t available or permission was refused. Use “Upload a file” instead.';
    }
  });

  stop.addEventListener('click', () => {
    if (recorder?.state === 'recording') recorder.stop();
    setRecordingState(false);
  });

  save.addEventListener('click', async () => {
    if (!recordingBlob) return;
    if (!recordingName.value.trim()) {
      recordingName.focus();
      status.textContent = 'Give this recording a short name before saving it.';
      return;
    }
    if (!permission.checked) {
      permission.focus();
      status.textContent = 'Confirm that you have permission to use this voice note before saving it.';
      return;
    }

    save.disabled = true;
    start.disabled = true;
    status.textContent = 'Saving your voice note privately…';
    const formData = new FormData();
    const extension = recordingBlob.type.includes('ogg') ? 'ogg' : 'webm';
    formData.append('File', recordingBlob, `voice-note-${new Date().toISOString().replace(/[:.]/g, '-')}.${extension}`);
    formData.append('DisplayName', recordingName.value.trim());
    formData.append('Attribution', attribution?.value || 'Recorded by project owner');
    formData.append('PermissionConfirmed', 'true');
    const token = uploadForm.querySelector('input[name="__RequestVerificationToken"]');
    if (token) formData.append('__RequestVerificationToken', token.value);

    try {
      const response = await window.mamnUpload(uploadForm.action, formData, save, 'voice note');
      hasUnsavedRecording = false;
      recordingBlob = undefined;
      review.hidden = true;
      permission.checked = false;
      recordingName.value = '';
      preview.removeAttribute('src');
      preview.load();
      if (previewUrl) URL.revokeObjectURL(previewUrl);
      previewUrl = undefined;
      time.textContent = '00:00';
      state.textContent = 'Saved to Sources';
      status.textContent = 'Your recording was saved privately. Opening your Sources…';
      window.mamnShowSources(response.url);
    } catch (error) {
      save.disabled = false;
      start.disabled = false;
      state.textContent = 'Recording not saved';
      status.textContent = `${error.message || 'The voice note could not be saved.'} Your recording is still available below so you can try again.`;
      save.disabled = false;
    }
  });

  window.addEventListener('beforeunload', event => {
    if (recorder?.state === 'recording' || hasUnsavedRecording) {
      event.preventDefault();
      event.returnValue = '';
    }
  });
})();

(() => {
  const dialog = document.querySelector('#help-dialog');
  const title = document.querySelector('#help-dialog-title');
  const content = document.querySelector('#help-dialog-content');
  if (!dialog || !title || !content) return;

  document.addEventListener('click', event => {
    const trigger = event.target.closest('[data-help-title]');
    if (trigger) {
      title.textContent = trigger.dataset.helpTitle;
      content.textContent = trigger.dataset.help;
      dialog.showModal();
      return;
    }
    if (event.target.closest('[data-help-close]')) dialog.close();
  });

  dialog.addEventListener('click', event => {
    if (event.target === dialog) dialog.close();
  });
})();

// Timing helper: set start/end from the audio player, play the selected stretch, and keep the player on the chosen recording.
(() => {
  const format = seconds => Number.isFinite(seconds) ? `${Math.floor(seconds / 60)}:${(seconds % 60).toFixed(1).padStart(4, '0')}` : '–';
  const round = seconds => Math.round(seconds * 10) / 10;

  document.querySelectorAll('[data-timing]').forEach(timing => {
    const form = timing.closest('form');
    const player = timing.querySelector('[data-timing-player]');
    const start = timing.querySelector('[data-timing-start]');
    const end = timing.querySelector('[data-timing-end]');
    const readout = timing.querySelector('[data-timing-readout]');
    const source = form?.querySelector('[data-timing-source]');
    if (!player || !start || !end) return;
    let stopAt;

    const update = () => {
      const a = parseFloat(start.value); const b = parseFloat(end.value);
      if (!readout) return;
      if (Number.isFinite(a) && Number.isFinite(b) && b > a) readout.textContent = `Selected ${format(a)} → ${format(b)} (${(b - a).toFixed(1)} s)`;
      else readout.textContent = 'End must be after start.';
    };
    start.addEventListener('input', update); end.addEventListener('input', update); update();

    timing.querySelectorAll('[data-set]').forEach(button => button.addEventListener('click', () => {
      const field = button.dataset.set === 'start' ? start : end;
      field.value = round(player.currentTime);
      field.dispatchEvent(new Event('input'));
      button.textContent = `${button.dataset.set === 'start' ? 'Start' : 'End'} set to ${format(player.currentTime)}`;
      setTimeout(() => { button.textContent = `Set ${button.dataset.set} from player`; }, 1800);
    }));

    timing.querySelector('[data-play-selection]')?.addEventListener('click', () => {
      const a = parseFloat(start.value) || 0; const b = parseFloat(end.value);
      player.currentTime = a; stopAt = Number.isFinite(b) && b > a ? b : undefined;
      player.play().catch(() => {});
    });
    player.addEventListener('timeupdate', () => { if (stopAt !== undefined && player.currentTime >= stopAt) { player.pause(); stopAt = undefined; } });

    source?.addEventListener('change', () => {
      const option = source.selectedOptions[0];
      if (option?.dataset.src && !player.src.endsWith(option.dataset.src)) { player.src = option.dataset.src; player.load(); }
    });
  });

  // Choosing saved words fills in the recording, timing and words, and marks the scene as quoted.
  document.querySelectorAll('[data-scene-form]').forEach(form => {
    const select = form.querySelector('[data-transcript-select]');
    if (!select) return;
    select.addEventListener('change', () => {
      const option = select.selectedOptions[0];
      const quoted = form.querySelector('input[name="TextOrigin"][value="0"]');
      const written = form.querySelector('input[name="TextOrigin"][value="1"]');
      if (!option?.value) { if (written) written.checked = true; return; }
      if (quoted) quoted.checked = true;
      const audio = form.querySelector('select[name="AudioAssetId"]');
      if (audio && option.dataset.asset) { audio.value = option.dataset.asset; audio.dispatchEvent(new Event('change')); }
      const start = form.querySelector('[data-timing-start]'); const end = form.querySelector('[data-timing-end]');
      if (start && option.dataset.start) { start.value = option.dataset.start; start.dispatchEvent(new Event('input')); }
      if (end && option.dataset.end) { end.value = option.dataset.end; end.dispatchEvent(new Event('input')); }
      const text = form.querySelector('[data-scene-text]');
      if (text && !text.value.trim() && option.dataset.text) text.value = option.dataset.text;
    });
  });
})();

// Slow actions (Live AI calls): show progress and prevent double submission.
(() => {
  document.querySelectorAll('button[data-busy-text]').forEach(button => {
    button.form?.addEventListener('submit', () => {
      button.textContent = button.dataset.busyText;
      button.setAttribute('aria-busy', 'true');
      setTimeout(() => { button.disabled = true; }, 0);
    });
  });
})();

// Story coach: copy AI-suggested words into the add-scene form, clearly marked as AI-suggested.
(() => {
  document.querySelectorAll('[data-use-narration]').forEach(button => {
    button.addEventListener('click', () => {
      const form = document.querySelector('#add-scene [data-scene-form]');
      if (!form) return;
      const text = form.querySelector('[data-scene-text]');
      if (text) text.value = button.dataset.useNarration;
      const origin = form.querySelector('input[name="TextOrigin"][value="2"]');
      if (origin) origin.checked = true;
      const select = form.querySelector('[data-transcript-select]');
      if (select) select.value = '';
      document.getElementById('add-scene')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      form.querySelector('input[name="Title"]')?.focus({ preventScroll: true });
    });
  });
})();

// Scene cards: drag by the handle to reorder (pointer events work for mouse and touch). Saves one new revision.
(() => {
  const list = document.querySelector('[data-sortable]');
  const form = document.querySelector('[data-reorder-form]');
  if (!list || !form) return;
  let dragging = null;
  const save = () => {
    form.querySelectorAll('input[name="order"]').forEach(x => x.remove());
    list.querySelectorAll('[data-scene-id]').forEach(card => {
      const input = document.createElement('input'); input.type = 'hidden'; input.name = 'order'; input.value = card.dataset.sceneId; form.appendChild(input);
    });
    form.submit();
  };
  list.querySelectorAll('[data-drag-handle]').forEach(handle => {
    handle.addEventListener('pointerdown', event => {
      dragging = handle.closest('[data-scene-id]');
      if (!dragging) return;
      event.preventDefault();
      handle.setPointerCapture(event.pointerId);
      dragging.classList.add('is-dragging');
      const start = [...list.children].indexOf(dragging);
      const move = e => {
        const over = document.elementFromPoint(e.clientX, e.clientY)?.closest('[data-scene-id]');
        if (!over || over === dragging) return;
        const rect = over.getBoundingClientRect();
        list.insertBefore(dragging, e.clientY < rect.top + rect.height / 2 ? over : over.nextSibling);
      };
      const end = () => {
        handle.removeEventListener('pointermove', move); handle.removeEventListener('pointerup', end); handle.removeEventListener('pointercancel', end);
        dragging.classList.remove('is-dragging');
        const moved = [...list.children].indexOf(dragging) !== start;
        dragging = null;
        if (moved) save();
      };
      handle.addEventListener('pointermove', move); handle.addEventListener('pointerup', end); handle.addEventListener('pointercancel', end);
    });
  });
})();

// Live render progress on Export: polls the owner's own job status and reloads once the film is ready.
(() => {
    const panel = document.querySelector('[data-render-progress]');
    if (!panel || !window.fetch) return;
    const url = panel.dataset.statusUrl;
    const note = panel.querySelector('[data-progress-note]');
    const steps = panel.querySelector('[data-progress-steps]');
    const elapsed = panel.querySelector('[data-progress-elapsed]');
    const bar = panel.querySelector('[data-progress-bar]');
    const fill = bar && bar.querySelector('span');
    let failures = 0;
    const time = s => s < 60 ? `${s} s` : `${Math.floor(s / 60)} min ${s % 60} s`;
    async function tick() {
        try {
            const response = await fetch(url, { headers: { 'Accept': 'application/json' }, cache: 'no-store', credentials: 'same-origin' });
            if (!response.ok) throw new Error(String(response.status));
            const job = await response.json(); failures = 0;
            if (job.state === 'succeeded' || job.state === 'failed' || job.state === 'cancelled' || job.state === 'none') {
                note.textContent = job.state === 'succeeded' ? 'Your film is ready — loading it…' : job.note || 'Finished';
                window.location.hash = job.state === 'succeeded' ? 'film' : 'render';
                window.location.reload();
                return;
            }
            note.textContent = job.note;
            const running = job.state === 'running' && job.total > 0;
            bar.classList.toggle('is-waiting', !running);
            const pct = running ? Math.round(100 * job.step / job.total) : 0;
            if (fill) fill.style.width = pct + '%';
            bar.setAttribute('aria-valuenow', String(pct));
            steps.textContent = running ? `Step ${Math.min(job.step + 1, job.total)} of ${job.total}.` : '';
            elapsed.textContent = job.state === 'queued'
                ? (job.waitingSeconds > 90 ? `Waiting ${time(job.waitingSeconds)} — the studio may be busy or restarting; it will start automatically.` : '')
                : `Working for ${time(job.elapsedSeconds)}.`;
        } catch {
            failures++;
            if (failures >= 3) elapsed.textContent = 'Can’t reach the studio right now — your film is still being made. Reload later to check.';
        }
        setTimeout(tick, failures ? 6000 : 2000);
    }
    setTimeout(tick, 1500);
})();

// Record a video: camera + microphone together, reviewed before it is saved through the normal private upload.
(() => {
  const $ = id => document.getElementById(id);
  const live = $('video-live'), cameraButton = $('video-camera'), flip = $('video-flip'), start = $('video-start'), stop = $('video-stop');
  const status = $('video-status'), state = $('video-state'), time = $('video-time'), indicator = $('video-indicator');
  const review = $('video-review'), preview = $('video-preview'), name = $('VideoDisplayName'), permission = $('VideoPermissionConfirmed'), save = $('video-save');
  const uploadForm = document.querySelector('#source-upload-form');
  if (!live || !cameraButton || !start || !stop || !review || !save || !uploadForm) return;
  const MAX_SECONDS = 120, MAX_BYTES = 20 * 1024 * 1024;
  if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) {
    cameraButton.disabled = true; status.textContent = 'This browser can’t record video here. Upload a clip instead.'; return;
  }
  const type = ['video/webm;codecs=vp9,opus', 'video/webm;codecs=vp8,opus', 'video/webm', 'video/mp4'].find(t => MediaRecorder.isTypeSupported(t)) || '';
  let stream, recorder, chunks = [], blob, url, facing = 'user', startedAt, tick, limit, unsaved = false;
  const clock = () => { const s = Math.floor((Date.now() - startedAt) / 1000); time.textContent = `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; };
  const stopTracks = () => { stream?.getTracks().forEach(t => t.stop()); stream = undefined; live.srcObject = null; live.hidden = true; };

  async function openCamera() {
    stopTracks();
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: facing, width: { ideal: 720 }, height: { ideal: 1280 }, frameRate: { ideal: 30, max: 30 } },
        audio: { echoCancellation: true, noiseSuppression: true }
      });
      live.srcObject = stream; live.hidden = false; live.classList.toggle('is-mirrored', facing === 'user');
      start.disabled = false; flip.hidden = false; cameraButton.textContent = 'Turn off camera';
      state.textContent = 'Camera on — ready'; status.textContent = 'Frame yourself, then press Start recording.';
    } catch {
      start.disabled = true; flip.hidden = true; cameraButton.textContent = 'Turn on camera'; state.textContent = 'Camera off';
      status.textContent = 'The camera or microphone isn’t available, or permission was refused. Upload a clip instead.';
    }
  }
  cameraButton.addEventListener('click', () => {
    if (stream && recorder?.state !== 'recording') { stopTracks(); start.disabled = true; flip.hidden = true; cameraButton.textContent = 'Turn on camera'; state.textContent = 'Camera off'; return; }
    openCamera();
  });
  flip?.addEventListener('click', () => { if (recorder?.state === 'recording') return; facing = facing === 'user' ? 'environment' : 'user'; openCamera(); });

  start.addEventListener('click', () => {
    if (!stream) return;
    review.hidden = true; if (url) URL.revokeObjectURL(url); blob = undefined; chunks = [];
    recorder = new MediaRecorder(stream, { ...(type ? { mimeType: type } : {}), videoBitsPerSecond: 1_000_000, audioBitsPerSecond: 96_000 });
    recorder.addEventListener('dataavailable', e => { if (e.data.size) chunks.push(e.data); });
    recorder.addEventListener('stop', () => {
      clearInterval(tick); clearTimeout(limit); indicator.classList.remove('is-recording');
      blob = new Blob(chunks, { type: recorder.mimeType || type || 'video/webm' });
      url = URL.createObjectURL(blob); preview.src = url; review.hidden = false; unsaved = true;
      stopTracks(); cameraButton.textContent = 'Turn on camera'; start.disabled = true; stop.disabled = true; flip.hidden = true; cameraButton.disabled = false;
      const mb = (blob.size / 1024 / 1024).toFixed(1);
      state.textContent = 'Video ready to watch';
      status.textContent = blob.size > MAX_BYTES ? `This video is ${mb} MiB, over the 20 MiB limit. Record a shorter one.` : `Watch it back (${mb} MiB), name it, confirm permission, then save it.`;
      save.disabled = blob.size > MAX_BYTES;
    });
    recorder.start(1000);
    startedAt = Date.now(); time.textContent = '00:00'; tick = setInterval(clock, 1000);
    limit = setTimeout(() => { if (recorder?.state === 'recording') recorder.stop(); }, MAX_SECONDS * 1000);
    indicator.classList.add('is-recording'); state.textContent = 'Recording'; start.disabled = true; stop.disabled = false; flip.hidden = true; cameraButton.disabled = true;
    status.textContent = 'Recording… press Stop and watch when you are done. Two minutes at most.';
  });
  stop.addEventListener('click', () => { if (recorder?.state === 'recording') recorder.stop(); });

  save.addEventListener('click', async () => {
    if (!blob) return;
    if (!name.value.trim()) { name.focus(); status.textContent = 'Give this video a short name before saving it.'; return; }
    if (!permission.checked) { permission.focus(); status.textContent = 'Confirm you have permission to use this video before saving it.'; return; }
    status.textContent = 'Saving your video privately — progress is shown under the Save button.';
    const extension = blob.type.includes('mp4') ? 'mp4' : 'webm';
    const data = new FormData();
    data.append('File', blob, `video-${new Date().toISOString().replace(/[:.]/g, '-')}.${extension}`);
    data.append('DisplayName', name.value.trim());
    data.append('Attribution', 'Recorded by project owner');
    data.append('PermissionConfirmed', 'true');
    const token = uploadForm.querySelector('input[name="__RequestVerificationToken"]'); if (token) data.append('__RequestVerificationToken', token.value);
    try {
      const response = await window.mamnUpload(uploadForm.action, data, save, 'video');
      unsaved = false; status.textContent = 'Saved privately. Opening your sources…';
      window.mamnShowSources(response.url);
    } catch (e) { save.disabled = false; status.textContent = `${e.message} Your video is still here so you can try again.`; }
  });
  window.addEventListener('beforeunload', e => { if (recorder?.state === 'recording' || unsaved) { e.preventDefault(); e.returnValue = ''; } });
  window.addEventListener('pagehide', stopTracks);
})();

// Confirm destructive actions marked with data-confirm (no inline handlers, so the content security policy can forbid them).
document.addEventListener('submit', event => {
  const form = event.target.closest('form[data-confirm]');
  if (form && !window.confirm(form.dataset.confirm)) event.preventDefault();
}, true);

// "Upload a file": same visible progress instead of a silent full-page post.
(() => {
  const form = document.getElementById('source-upload-form');
  const button = form?.querySelector('button[type="submit"]');
  if (!form || !button || !window.XMLHttpRequest || !window.FormData) return;
  form.addEventListener('submit', async event => {
    if (event.defaultPrevented || !form.checkValidity()) return;
    event.preventDefault();
    try {
      const file = form.querySelector('input[type="file"]')?.files?.[0];
      const what = file?.type.startsWith('video/') ? 'video' : file?.type.startsWith('image/') ? 'picture' : 'recording';
      const result = await window.mamnUpload(form.action, new FormData(form), button, what);
      window.mamnShowSources(result.url);
    } catch { /* the message is shown beside the button */ }
  });
})();

// Make a slide: live preview of the title card while typing.
(() => {
  const form = document.querySelector('[data-slide-form]'); const preview = document.querySelector('[data-slide-preview]');
  if (!form || !preview) return;
  const title = form.querySelector('[data-slide-title]'), subtitle = form.querySelector('[data-slide-subtitle]');
  const update = () => {
    preview.querySelector('[data-preview-title]').textContent = title.value.trim() || 'Your title';
    preview.querySelector('[data-preview-subtitle]').textContent = subtitle.value.trim();
    const theme = form.querySelector('[data-slide-theme]:checked')?.dataset.slideTheme || 'night';
    preview.className = `slide-preview ${theme}`;
  };
  form.addEventListener('input', update); form.addEventListener('change', update); update();
  form.addEventListener('submit', () => { const b = form.querySelector('button[type="submit"]'); if (form.checkValidity()) { b.disabled = true; b.textContent = 'Making your slide…'; } });
})();
