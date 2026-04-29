// Admin page watch helper: listens for DNAsession-admin changes and redirects
window.addEventListener('storage', (e) => {
    if (e.key === 'DNASession-admin') {
        try {
            const s = JSON.parse(e.newValue || '{}');
            if (!s || typeof s.isAdmin === 'undefined') return;
            if (!s.isAdmin) {
                // if admin role lost redirect to shop
                window.location.href = '/Shop';
            }
        } catch (err) { /* ignore */ }
    }
});
