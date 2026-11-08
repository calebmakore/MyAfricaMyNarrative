(() => {
  const start = document.querySelector('#start-recording');
  const stop = document.querySelector('#stop-recording');
  const status = document.querySelector('#recording-status');
  const preview = document.querySelector('#recording-preview');
  if (!start || !stop || !status || !preview) return;
  if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) {
    start.disabled = true;
    status.textContent = 'This browser cannot record here. Use the working file upload above.';
    return;
  }
  let recorder, stream, chunks = [];
  start.addEventListener('click', async () => {
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      recorder = new MediaRecorder(stream);
      chunks = [];
      recorder.addEventListener('dataavailable', event => { if (event.data.size) chunks.push(event.data); });
      recorder.addEventListener('stop', () => {
        preview.src = URL.createObjectURL(new Blob(chunks, { type: recorder.mimeType }));
        preview.hidden = false;
        status.textContent = 'Review your recording. To preserve it, download it from the player and upload it as a source.';
        stream.getTracks().forEach(track => track.stop());
      });
      recorder.start(); start.disabled = true; stop.disabled = false; status.textContent = 'Recording… Stop when you are ready to review.';
    } catch { status.textContent = 'Microphone permission was refused or unavailable. Use the file upload above.'; }
  });
  stop.addEventListener('click', () => { if (recorder?.state === 'recording') recorder.stop(); start.disabled = false; stop.disabled = true; });
  window.addEventListener('beforeunload', event => { if (recorder?.state === 'recording') { event.preventDefault(); event.returnValue = ''; } });
})();
