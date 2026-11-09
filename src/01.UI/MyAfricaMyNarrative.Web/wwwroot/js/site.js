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
    status.textContent = 'This browser cannot record here. Use the working file upload above.';
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
    start.textContent = isRecording ? 'Recording in progress' : 'Start microphone';
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
        status.textContent = 'Review your voice note, confirm permission beside it, then choose “Save recording to Sources”.';
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
      status.textContent = 'Microphone permission was refused or unavailable. Use the file upload above.';
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
      const response = await fetch(uploadForm.action, { method: 'POST', body: formData, credentials: 'same-origin' });
      const html = await response.text();
      const documentResult = new DOMParser().parseFromString(html, 'text/html');
      const serverError = documentResult.querySelector('.notice.error')?.textContent?.trim();
      if (!response.ok || serverError) throw new Error(serverError || 'The server could not save this recording.');
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
      window.location.assign(`${response.url.split('#')[0]}#sources`);
    } catch (error) {
      save.disabled = false;
      start.disabled = false;
      state.textContent = 'Recording not saved';
      status.textContent = `${error.message || 'The voice note could not be saved.'} Your recording is still available below so you can try again.`;
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
