(function(){
  if (window._sharedUIInstalled) return; window._sharedUIInstalled = true;

  // Create DOM elements for alert/confirm/input/toast
  function ensureElements(){
    if (document.getElementById('shared-ui-overlay')) return;
    const overlay = document.createElement('div'); overlay.id = 'shared-ui-overlay'; overlay.style.position='fixed'; overlay.style.inset='0'; overlay.style.display='none'; overlay.style.alignItems='center'; overlay.style.justifyContent='center'; overlay.style.background='rgba(2,6,23,0.45)'; overlay.style.zIndex='12000';

    // Alert / Confirm / Input container
    const box = document.createElement('div'); box.id='shared-ui-box'; box.style.minWidth='320px'; box.style.maxWidth='90%'; box.style.background='linear-gradient(180deg,#fff,#fbfdff)'; box.style.border='1px solid rgba(15,23,42,0.06)'; box.style.borderRadius='12px'; box.style.boxShadow='0 20px 40px rgba(2,6,23,0.25)'; box.style.padding='18px'; box.style.display='flex'; box.style.flexDirection='column'; box.style.gap='12px';

    // Header bar (gradient) with title and close
    const header = document.createElement('div'); header.style.display='flex'; header.style.alignItems='center'; header.style.justifyContent='space-between'; header.style.padding='12px 16px'; header.style.borderRadius='10px 10px 0 0'; header.style.background='linear-gradient(90deg,#10b981,#3b82f6)'; header.style.color='#fff';
    const title = document.createElement('div'); title.id='shared-ui-title'; title.style.fontSize='15px'; title.style.fontWeight='700'; title.textContent = 'Notice';
    const close = document.createElement('button'); close.id='shared-ui-close'; close.innerHTML = '&#10005;'; close.style.background='transparent'; close.style.border='none'; close.style.color='rgba(255,255,255,0.95)'; close.style.fontSize='18px'; close.style.cursor='pointer';
    header.appendChild(title); header.appendChild(close);

    const body = document.createElement('div'); body.id='shared-ui-body'; body.style.color='#0f172a'; body.style.fontSize='14px'; body.style.padding='18px 16px'; body.style.minHeight='20px'; backgroundFallback: body.style.background = '#fff';
    const input = document.createElement('input'); input.id='shared-ui-input'; input.style.display='none'; input.type='text'; input.className='form-input'; input.style.padding='12px 14px'; input.style.borderRadius='10px'; input.style.border='1px solid #e6edf8'; input.style.width='100%'; input.style.boxShadow='inset 0 1px 0 rgba(0,0,0,0.03)';

    const actions = document.createElement('div'); actions.id='shared-ui-actions'; actions.style.display='flex'; actions.style.justifyContent='flex-end'; actions.style.gap='10px'; actions.style.padding='12px 16px 16px 16px';
    const btnCancel = document.createElement('button'); btnCancel.id='shared-ui-cancel'; btnCancel.textContent='Cancel'; btnCancel.style.background='#fff'; btnCancel.style.border='1px solid rgba(15,23,42,0.06)'; btnCancel.style.padding='8px 14px'; btnCancel.style.borderRadius='10px'; btnCancel.style.cursor='pointer'; btnCancel.style.fontWeight='700';
    const btnOk = document.createElement('button'); btnOk.id='shared-ui-ok'; btnOk.textContent='OK'; btnOk.style.background='linear-gradient(90deg,#6366f1,#4338ca)'; btnOk.style.color='#fff'; btnOk.style.border='none'; btnOk.style.padding='8px 16px'; btnOk.style.borderRadius='10px'; btnOk.style.cursor='pointer'; btnOk.style.fontWeight='800';

    actions.appendChild(btnCancel); actions.appendChild(btnOk);
    box.appendChild(header); box.appendChild(body); box.appendChild(input); box.appendChild(actions);
    overlay.appendChild(box);
    document.body.appendChild(overlay);

    // Toast container
    const toasts = document.createElement('div'); toasts.id='shared-ui-toasts'; toasts.style.position='fixed'; toasts.style.right='20px'; toasts.style.bottom='28px'; toasts.style.display='flex'; toasts.style.flexDirection='column'; toasts.style.gap='10px'; toasts.style.zIndex='11999'; document.body.appendChild(toasts);

    // Basic handlers
    btnOk.addEventListener('click', ()=>{ overlay._ok && overlay._ok(); });
    btnCancel.addEventListener('click', ()=>{ overlay._cancel && overlay._cancel(); });
    close.addEventListener('click', ()=>{ overlay._cancel && overlay._cancel(); });
    overlay.addEventListener('click', (e)=>{ if (e.target === overlay) overlay._cancel && overlay._cancel(); });
  }

  function showAlert(msg, title){
    try{
      ensureElements();
      const overlay = document.getElementById('shared-ui-overlay');
      const t = document.getElementById('shared-ui-title');
      const b = document.getElementById('shared-ui-body');
      const input = document.getElementById('shared-ui-input');
      t.textContent = title || 'Notice';
      b.innerHTML = String(msg).replace(/\n/g,'<br>');
      input.style.display='none';
      overlay.style.display='flex';
      // ensure focus visible look
      const okBtn = document.getElementById('shared-ui-ok'); if (okBtn) okBtn.focus();
      overlay._ok = () => { overlay.style.display='none'; overlay._ok = overlay._cancel = null; };
      overlay._cancel = overlay._ok;
    }catch(e){}
  }

  function showConfirm(msg, title){
    return new Promise((resolve)=>{
      try{
        ensureElements();
        const overlay = document.getElementById('shared-ui-overlay');
        const t = document.getElementById('shared-ui-title');
        const b = document.getElementById('shared-ui-body');
        const input = document.getElementById('shared-ui-input');
        t.textContent = title || 'Confirm';
        b.innerHTML = String(msg).replace(/\n/g,'<br>');
        input.style.display='none';
        overlay.style.display='flex';
        function ok(){ overlay.style.display='none'; cleanup(); resolve(true); }
        function cancel(){ overlay.style.display='none'; cleanup(); resolve(false); }
        function cleanup(){ overlay._ok = overlay._cancel = null; }
        overlay._ok = ok; overlay._cancel = cancel;
      }catch(e){ resolve(false);}    
    });
  }

  function showInputModal(titleText, placeholder){
    return new Promise((resolve)=>{
      try{
        ensureElements();
        const overlay = document.getElementById('shared-ui-overlay');
        const t = document.getElementById('shared-ui-title');
        const b = document.getElementById('shared-ui-body');
        const input = document.getElementById('shared-ui-input');
        t.textContent = titleText || 'Enter value';
        b.innerHTML = '';
        input.style.display='block'; input.placeholder = placeholder || ''; input.value = '';
        overlay.style.display='flex';
        setTimeout(()=> input.focus(),80);
        function ok(){ const v = input.value?.trim(); overlay.style.display='none'; cleanup(); resolve(v || null); }
        function cancel(){ overlay.style.display='none'; cleanup(); resolve(null); }
        function cleanup(){ overlay._ok = overlay._cancel = null; }
        overlay._ok = ok; overlay._cancel = cancel;
        // keyboard
        function keyHandler(e){ if (e.key === 'Enter') ok(); if (e.key === 'Escape') cancel(); }
        document.addEventListener('keydown', keyHandler);
        // cleanup after close
        const origCancel = overlay._cancel;
        overlay._cancel = ()=>{ document.removeEventListener('keydown', keyHandler); origCancel && origCancel(); };
      }catch(e){ resolve(null); }
    });
  }

  function showToast(msg, timeout=3000){
    try{
      ensureElements();
      const cont = document.getElementById('shared-ui-toasts');
      const t = document.createElement('div'); t.className='shared-ui-toast'; t.style.background='linear-gradient(90deg,#10b981,#3b82f6)'; t.style.color='#fff'; t.style.padding='10px 14px'; t.style.borderRadius='10px'; t.style.boxShadow='0 8px 20px rgba(2,6,23,0.2)'; t.style.fontWeight='600'; t.innerHTML = msg;
      cont.appendChild(t);
      setTimeout(()=>{ t.style.opacity='0'; t.style.transform='translateX(12px)'; setTimeout(()=> t.remove(),300); }, timeout);
    }catch(e){}
  }

  // expose globally
  window.showCustomAlert = showAlert;
  window.showCustomToast = showToast;
  window.showCustomConfirm = showConfirm;
  window.showCustomInput = showInputModal;

})();
