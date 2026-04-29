// ===== DNA Softech - Site-wide JS (site.js) =====
// Handles auth state display, cart badge, user dropdown, and toast notifications
// for pages that use _Layout.cshtml (Login, Product Detail, Checkout Success, etc.)

(function () {
    'use strict';

    // ── Auth state ──────────────────────────────────────────────────────
    function getUser() {
        try { return JSON.parse(localStorage.getItem('currentUser') || 'null'); } catch { return null; }
    }

    function refreshAuthUI() {
        var user = getUser();
        var anonMenu = document.getElementById('user-menu-anon');
        var authMenu = document.getElementById('user-menu-auth');
        var userName = document.getElementById('nav-user-name');
        var adminItem = document.getElementById('nav-admin-item');

        if (user && user.email) {
            if (anonMenu) anonMenu.style.display = 'none';
            if (authMenu) authMenu.style.display = '';
            if (userName) userName.textContent = user.name ? user.name.split(' ')[0] : 'Account';
            var navAvatar = document.getElementById('nav-user-avatar');
            if (navAvatar) {
                if (user.profilePhotoUrl) {
                    navAvatar.innerHTML = '<img src="' + user.profilePhotoUrl + '" style="width:100%;height:100%;object-fit:cover;border-radius:50%;" />';
                } else {
                    navAvatar.textContent = user.name ? user.name.charAt(0).toUpperCase() : 'U';
                }
            }
            if (adminItem) {
                var isAdmin = user.isAdmin === true || (user.role && user.role.toLowerCase() === 'admin') ||
                    (user.email && user.email.toLowerCase() === 'admin@dnasoftech.com');
                adminItem.style.display = isAdmin ? '' : 'none';
            }
        } else {
            if (anonMenu) anonMenu.style.display = '';
            if (authMenu) authMenu.style.display = 'none';
        }
    }

    // ── Cart badge ──────────────────────────────────────────────────────
    function refreshCartBadge() {
        var badge = document.getElementById('cart-badge');
        if (!badge) return;
        try {
            var cart = JSON.parse(localStorage.getItem('cart') || '[]');
            var count = Array.isArray(cart) ? cart.reduce(function (s, i) { return s + (i.quantity || 1); }, 0) : 0;
            badge.textContent = count;
            badge.style.display = count > 0 ? '' : 'none';
        } catch {
            badge.style.display = 'none';
        }
    }

    // ── User dropdown toggle ────────────────────────────────────────────
    function initDropdown() {
        var trigger = document.getElementById('user-dropdown-btn');
        var menu = document.getElementById('user-dropdown-menu');
        if (!trigger || !menu) return;

        trigger.addEventListener('click', function (e) {
            e.stopPropagation();
            var expanded = trigger.getAttribute('aria-expanded') === 'true';
            trigger.setAttribute('aria-expanded', !expanded);
            menu.style.display = expanded ? 'none' : 'block';
        });

        document.addEventListener('click', function () {
            trigger.setAttribute('aria-expanded', 'false');
            menu.style.display = 'none';
        });
    }

    // ── Cart slide-out panel ────────────────────────────────────────────
    function initCartPanel() {
        var toggleBtn = document.getElementById('cart-toggle-btn');
        var closeBtn = document.getElementById('cart-close-btn');
        var overlay = document.getElementById('cart-overlay');
        var panel = document.getElementById('cart-panel');
        if (!panel) return;

        function open() {
            panel.setAttribute('aria-hidden', 'false');
            panel.classList.add('open');
            if (overlay) { overlay.setAttribute('aria-hidden', 'false'); overlay.classList.add('active'); }
            renderCartPanel();
        }
        function close() {
            panel.setAttribute('aria-hidden', 'true');
            panel.classList.remove('open');
            if (overlay) { overlay.setAttribute('aria-hidden', 'true'); overlay.classList.remove('active'); }
        }

        if (toggleBtn) toggleBtn.addEventListener('click', open);
        if (closeBtn) closeBtn.addEventListener('click', close);
        if (overlay) overlay.addEventListener('click', close);
    }

    function renderCartPanel() {
        var list = document.getElementById('cart-items-list');
        var emptyMsg = document.getElementById('cart-empty-msg');
        var footer = document.getElementById('cart-panel-footer');
        var totalEl = document.getElementById('cart-total-value');
        if (!list) return;

        var cart = [];
        try { cart = JSON.parse(localStorage.getItem('cart') || '[]'); } catch { }
        list.innerHTML = '';

        if (!cart.length) {
            if (emptyMsg) emptyMsg.style.display = '';
            if (footer) footer.style.display = 'none';
            return;
        }
        if (emptyMsg) emptyMsg.style.display = 'none';
        if (footer) footer.style.display = '';

        var total = 0;
        cart.forEach(function (item) {
            var price = item.price || 0;
            var qty = item.quantity || 1;
            total += price * qty;
            var li = document.createElement('li');
            li.style.cssText = 'display:flex;gap:12px;padding:12px 0;border-bottom:1px solid #e2e8f0;';
            li.innerHTML =
                '<div style="flex:1;min-width:0;">' +
                '<div style="font-weight:600;font-size:14px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">' + (item.name || 'Product') + '</div>' +
                '<div style="font-size:13px;color:#64748b;">Qty: ' + qty + ' × ₹' + Number(price).toLocaleString('en-IN') + '</div>' +
                '</div>' +
                '<div style="font-weight:700;white-space:nowrap;">₹' + Number(price * qty).toLocaleString('en-IN') + '</div>';
            list.appendChild(li);
        });

        if (totalEl) totalEl.textContent = '₹' + Number(total).toLocaleString('en-IN');
    }

    // ── Toast ───────────────────────────────────────────────────────────
    window.showSiteToast = function (message, type) {
        var container = document.getElementById('toast-container');
        if (!container) return;
        var toast = document.createElement('div');
        toast.className = 'site-toast ' + (type || 'success');
        toast.textContent = message;
        container.appendChild(toast);
        requestAnimationFrame(function () { toast.classList.add('show'); });
        setTimeout(function () {
            toast.classList.remove('show');
            setTimeout(function () { toast.remove(); }, 300);
        }, 3000);
    };

    // Expose so other scripts (e.g. product-detail.js) can refresh the badge
    window.refreshCartBadge = refreshCartBadge;

    // ── Listen for storage changes ──────────────────────────────────────
    window.addEventListener('storage', function (e) {
        if (e.key === 'currentUser') refreshAuthUI();
        if (e.key === 'cart') refreshCartBadge();
        if (e.key === 'DNASession-logged-out') {
            refreshAuthUI();
            refreshCartBadge();
        }
    });

    // ── Init on DOM ready ───────────────────────────────────────────────
    document.addEventListener('DOMContentLoaded', function () {
        refreshAuthUI();
        refreshCartBadge();
        initDropdown();
        initCartPanel();
    });
})();
