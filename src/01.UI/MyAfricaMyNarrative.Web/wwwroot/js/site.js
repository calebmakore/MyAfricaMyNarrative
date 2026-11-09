(() => {
  const start = document.querySelector('#start-recording');
  const stop = document.querySelector('#stop-recording');
  const save = document.querySelector('#save-recording');
  const status = document.querySelector('#recording-status');
  const preview = document.querySelector('#recording-preview');
  const review = document.querySelector('#recording-review');
  const uploadForm = document.querySelector('#source-upload-form');
  const attribution = document.querySelector('#Attribution');
  const permission = document.querySelector('#PermissionConfirmed');
  if (!start || !stop || !save || !status || !preview || !review || !uploadForm || !permission) return;

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
  let chunks = [];
  let hasUnsavedRecording = false;

  const setRecordingState = isRecording => {
    start.disabled = isRecording;
    stop.disabled = !isRecording;
  };

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
        const contentType = recorder.mimeType || 'audio/webm';
        recordingBlob = new Blob(chunks, { type: contentType });
        previewUrl = URL.createObjectURL(recordingBlob);
        preview.src = previewUrl;
        review.hidden = false;
        hasUnsavedRecording = true;
        status.textContent = 'Review your voice note, confirm permission above, then choose “Use this recording” to add it privately to this project.';
        stopTracks();
      });
      recorder.start();
      setRecordingState(true);
      status.textContent = 'Recording… Stop when you are ready to review. The maximum recording length is five minutes.';
      maximumDurationTimer = window.setTimeout(() => {
        if (recorder?.state === 'recording') recorder.stop();
        setRecordingState(false);
      }, 5 * 60 * 1000);
    } catch {
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
    formData.append('Attribution', attribution?.value || 'Recorded by project owner');
    formData.append('PermissionConfirmed', 'true');
    const token = uploadForm.querySelector('input[name="__RequestVerificationToken"]');
    if (token) formData.append('__RequestVerificationToken', token.value);

    try {
      const response = await fetch(uploadForm.action, { method: 'POST', body: formData, credentials: 'same-origin' });
      if (!response.ok) throw new Error('Upload failed');
      hasUnsavedRecording = false;
      window.location.reload();
    } catch {
      save.disabled = false;
      start.disabled = false;
      status.textContent = 'The voice note could not be saved. Your recording is still available below—check your connection and try again.';
    }
  });

  window.addEventListener('beforeunload', event => {
    if (recorder?.state === 'recording' || hasUnsavedRecording) {
      event.preventDefault();
      event.returnValue = '';
    }
  });
})();
