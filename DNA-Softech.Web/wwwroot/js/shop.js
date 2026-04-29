// ===== CONFIGURATION =====
const apiBase = '';

// ===== STATE MANAGEMENT =====
const state = {
    products: [],
    filteredProducts: [],
    cart: [],
    wishlist: [],
    currentUser: null,
    selectedProduct: null,
    currentCategory: 'all',
    currentView: 'grid',
    filters: {
        searchTerm: '',
        minPrice: null,
        maxPrice: null,
        rating: [],
        inStock: false,
        sortBy: 'popularity'
    }
};

// lightweight debug helper to avoid errors when debug element is missing
function setDebug(text) {
    try {
        const dbg = document.getElementById('debugInfo');
        if (!dbg) return;
        dbg.style.display = 'block';
        dbg.textContent = text;
    } catch (e) { /* ignore */ }
}

// Ensure toggleView is available early so inline onclick handlers won't fail
window.toggleView = function (v) {
    try {
        state.currentView = v === 'list' ? 'list' : 'grid';
        document.querySelectorAll('.view-btn').forEach(b => b.classList.toggle('active', b.getAttribute('data-view') === state.currentView));
        const container = document.getElementById('productsContainer');
        if (container) {
            if (state.currentView === 'list') container.classList.add('list-view');
            else container.classList.remove('list-view');
        }
    } catch (e) { /* ignore */ }
};

// Render per-card cart control: either 'Add to Cart' button or quantity controls
function renderProductCardControl(product, cardElement) {
    try {
        const ctrl = cardElement.querySelector('.cart-control');
        if (!ctrl) return;
        const pid = product.id;
        ctrl.innerHTML = '';
        const item = state.cart.find(i => String(i.productId) === String(pid));
        // If product is out of stock, show disabled Out of Stock control
        if (!product.inStock) {
            const ds = document.createElement('div');
            ds.className = 'out-of-stock-control';
            ds.style.color = '#ffffff';
            ds.style.background = 'var(--danger)';
            ds.style.padding = '8px 12px';
            ds.style.borderRadius = '8px';
            ds.style.fontWeight = '700';
            ds.style.display = 'inline-block';
            ds.textContent = 'Out of Stock';
            ctrl.appendChild(ds);
            return;
        }


        if (!item) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn-add-cart';
            btn.setAttribute('data-id', pid);
            btn.innerHTML = '<i class="fas fa-shopping-cart"></i> Add to Cart';
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                // ensure product exists before adding (products should be loaded when card rendered)
                addToCart(pid, 1);
            });
            ctrl.appendChild(btn);
        } else {
            const wrap = document.createElement('div');
            wrap.className = 'card-qty-controls';
            wrap.style.display = 'inline-flex';
            wrap.style.alignItems = 'center';
            wrap.style.gap = '8px';

            const dec = document.createElement('button');
            dec.className = 'qty-decr';
            dec.textContent = '-';
            dec.addEventListener('click', (e) => {
                e.stopPropagation();
                // read latest quantity from state to avoid stale closure
                const cur = state.cart.find(i => String(i.productId) === String(pid));
                const curQty = cur ? (cur.quantity || 0) : 0;
                updateCartQuantity(pid, curQty - 1);
            });

            const q = document.createElement('div');
            q.className = 'card-qty';
            q.textContent = item.quantity;
            q.style.minWidth = '36px'; q.style.textAlign = 'center'; q.style.fontWeight = '700';

            const inc = document.createElement('button');
            inc.className = 'qty-incr';
            inc.textContent = '+';
            inc.addEventListener('click', (e) => {
                e.stopPropagation();
                const cur = state.cart.find(i => String(i.productId) === String(pid));
                const curQty = cur ? (cur.quantity || 0) : 0;
                updateCartQuantity(pid, curQty + 1);
            });

            wrap.appendChild(dec);
            wrap.appendChild(q);
            wrap.appendChild(inc);
            ctrl.appendChild(wrap);
        }
    } catch (e) { /* ignore */ }
}

// Quick view placeholder - opens product detail page
function quickView(e, id) {
    if (e) e.stopPropagation();
    window.location.href = `/Shop/Product/${id}`;
}

// Open product modal placeholder
function openProductModal(id) {
    window.location.href = `/Shop/Product/${id}`;
}

function navigateToProduct(id) {
    if (!id) return;
    const pid = Number(id) || id;
    window.location.href = `/Shop/Product/${pid}`;
}

// Load current user profile if available and update UI
async function loadCurrentUser() {
    try {
        const r = await fetch('/api/users/me', { credentials: 'include' });
        if (r.ok) {
            const u = await r.json();
            // check admin status separately
            try {
                const ch = await fetch('/api/admin/check', { credentials: 'include' });
                u.isAdmin = ch.ok;
            } catch { u.isAdmin = false; }
            state.currentUser = u;
            await hydrateUserStateFromServer();
            saveToLocalStorage();
            refreshAuthUI();
        } else if (r.status === 401) {
            // Only clear session on definitive "not authenticated" — not on 500/503/network errors
            state.currentUser = null;
            saveToLocalStorage();
            refreshAuthUI();
        }
        // On 500/503/etc: keep the existing localStorage user so UI stays logged-in
    } catch (e) { /* ignore network errors — keep localStorage user */ }
}

async function hydrateUserStateFromServer() {
    if (!state.currentUser) return;

    try {
        const [wishlistRes, cartRes] = await Promise.all([
            fetch('/api/wishlist', { credentials: 'include' }),
            fetch('/api/cart', { credentials: 'include' })
        ]);

        if (wishlistRes.ok) {
            const wishlist = await wishlistRes.json();
            if (Array.isArray(wishlist)) state.wishlist = wishlist;
        }

        if (cartRes.ok) {
            const cartItems = await cartRes.json();
            if (Array.isArray(cartItems)) {
                state.cart = cartItems.map(i => ({ productId: String(i.productId), quantity: i.quantity }));
            }
        }

        saveToLocalStorage();
        renderCart();
        renderWishlist();
    } catch (e) {
        // keep local values if server sync fails
    }
}

function refreshAuthUI() {
    updateUserUI();
    updateAdminLink();
}

// Show or hide admin link based on user admin status or seeded admin email
function updateAdminLink() {
    const link = document.getElementById('adminLink');
    const myOrders = document.getElementById('myOrdersLink');
    if (!link) return;
    const u = state.currentUser;
    if (myOrders) myOrders.style.display = u ? 'inline-flex' : 'none';
    if (u && (u.isAdmin === true || (u.email && u.email.toLowerCase() === 'admin@dnasoftech.com'))) {
        link.style.display = 'inline-flex';
    } else {
        link.style.display = 'none';
    }
    // Notify other tabs about admin status change so admin pages can react immediately
    try {
        const adminState = { isAdmin: !!(u && (u.isAdmin === true || (u.email && u.email.toLowerCase() === 'admin@dnasoftech.com'))), ts: Date.now() };
        localStorage.setItem('DNASession-admin', JSON.stringify(adminState));
    } catch (e) { /* ignore storage errors */ }
}
function createProductCard(product) {
    const discountPerc = product.discount || 0;
    return `
    <div class="product-card reveal">
        ${discountPerc > 0 ? `<div class="product-badges"><span class="discount-badge">${discountPerc}% OFF</span></div>` : ''}
        <div class="wishlist-icon" onclick="toggleWishlist(${product.id})">
            <i class="far fa-heart"></i>
        </div>
        <div class="product-image-wrapper">
            <img src="${product.image || 'https://via.placeholder.com/500?text=No+Image'}" alt="${product.name}" class="product-image">
        </div>
        <div class="product-body">
            <div class="product-category">${product.category || 'Product'}</div>
            <h3 class="product-title">${product.name}</h3>
            <div class="product-rating">
                <div class="stars">${generateStars(product.rating || 0)}</div>
                <span class="rating-count">(${product.reviews || 0})</span>
            </div>
            <div class="trust-signals">
                <span class="stock-indicator ${product.inStock ? 'in' : 'out'}">
                    <i class="fas fa-circle"></i> ${product.inStock ? 'In Stock' : 'Out of Stock'}
                </span>
                <span class="delivery-text"><i class="fas fa-truck"></i> Free delivery by Mar 30</span>
            </div>
            <div class="product-price-row">
                <div class="price-main">
                    <span class="product-price">â‚¹${Number(product.price || 0).toLocaleString('en-IN')}</span>
                    ${product.originalPrice ? `
                    <div class="price-line">
                        <span class="product-mrp"><span class="product-original-price">â‚¹${Number(product.originalPrice).toLocaleString('en-IN')}</span></span>
                    </div>
                    ` : ''}
                </div>
                ${discountPerc > 0 ? `<span class="save-percent">${discountPerc}% OFF</span>` : ''}
            </div>
            <div class="product-footer">
                <button class="btn-add-cart" onclick="event.stopPropagation(); addToCart(${product.id}, 1)">
                    <i class="fas fa-shopping-cart"></i> ADD TO CART
                </button>
                <button class="btn-quick-view" onclick="event.stopPropagation(); quickView(${product.id})">
                    <i class="fas fa-eye"></i>
                </button>
            </div>
        </div>
    </div>`;
}

// run on load after DOM ready
loadFromLocalStorage();
// Attach critical header button listeners immediately so they work before async init() completes
document.addEventListener('DOMContentLoaded', () => {
    const userBtn = document.getElementById('userBtn');
    if (userBtn) userBtn.addEventListener('click', () => openUserModal());
    const cartToggle = document.getElementById('cartToggle');
    if (cartToggle) cartToggle.addEventListener('click', () => toggleCart());
    const wishlistBtn = document.getElementById('wishlistBtn');
    if (wishlistBtn) wishlistBtn.addEventListener('click', () => toggleWishlistPanel());
});


// Listen for logout notification from admin tab and clear local state
window.addEventListener('storage', (e) => {
    if (e.key === 'DNASession-logged-out') {
        try { localStorage.clear(); } catch (e) { }
        try { sessionStorage.clear(); } catch (e) { }
        state.currentUser = null;
        state.cart = [];
        state.wishlist = [];
        renderCart();
        renderWishlist();
        refreshAuthUI();
    }
});

// Exposed logout helper for main shop UI
async function logoutEverything() {
    try { await fetch('/api/users/logout', { method: 'POST', credentials: 'include' }); } catch (e) { }
    try { localStorage.clear(); sessionStorage.clear(); } catch (e) { }
    try { document.cookie = 'DNASession=; Path=/; Expires=Thu, 01 Jan 1970 00:00:01 GMT; SameSite=None; Secure'; } catch (e) { }
    state.currentUser = null;
    state.cart = [];
    state.wishlist = [];
    renderCart();
    renderWishlist();
    // Ensure admin flags are explicitly removed and UI updated
    try { localStorage.removeItem('DNASession-admin'); } catch { };
    try { localStorage.removeItem('currentUser'); } catch { };
    refreshAuthUI();
    // Ensure DOM elements are hidden immediately in-case of stale state
    try {
        const a = document.getElementById('adminLink'); if (a) a.style.display = 'none';
        const m = document.getElementById('myOrdersLink'); if (m) m.style.display = 'none';
        const badge = document.getElementById('adminBadge'); if (badge) badge.style.display = 'none';
    } catch (e) { }
    // notify other tabs
    try { localStorage.setItem('DNASession-logged-out', String(Date.now())); } catch (e) { }
}

// ===== UTILITY FUNCTIONS =====
function money(v) {
    try {
        return '\u20B9' + Number(v).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    } catch (e) {
        return '\u20B9' + Number(v).toFixed(2);
    }
}

function generateStars(rating) {
    const fullStars = Math.floor(rating);
    const halfStar = rating % 1 >= 0.5;
    let stars = '';

    for (let i = 0; i < fullStars; i++) {
        stars += '<i class="fas fa-star"></i>';
    }
    if (halfStar) {
        stars += '<i class="fas fa-star-half-alt"></i>';
    }
    for (let i = fullStars + (halfStar ? 1 : 0); i < 5; i++) {
        stars += '<i class="far fa-star"></i>';
    }
    return stars;
}

function showToast(message, type = 'success') {
    const toast = document.getElementById('toast');
    toast.textContent = message;
    toast.className = `toast show ${type}`;

    setTimeout(() => {
        toast.classList.remove('show');
    }, 3000);
}

function showMiniToast(message) {
    const mt = document.getElementById('miniToast');
    mt.textContent = message;
    mt.classList.add('show');
    setTimeout(() => mt.classList.remove('show'), 1600);
}

function saveToLocalStorage() {
    localStorage.setItem('cart', JSON.stringify(state.cart));
    localStorage.setItem('wishlist', JSON.stringify(state.wishlist));
    localStorage.setItem('currentUser', JSON.stringify(state.currentUser));
}

function loadFromLocalStorage() {
    const cart = localStorage.getItem('cart');
    const wishlist = localStorage.getItem('wishlist');
    const currentUser = localStorage.getItem('currentUser');

    if (cart) state.cart = JSON.parse(cart);
    if (wishlist) state.wishlist = JSON.parse(wishlist);
    if (currentUser) state.currentUser = JSON.parse(currentUser);

    // Normalize cart: keep productId as string but preserve any enriched fields
    if (Array.isArray(state.cart)) {
        state.cart = state.cart.map(i => ({
            ...i,
            productId: String(i.productId),
            quantity: i.quantity || 1
        }));
    }
}

function updateUserUI() {
    const userNameSpan = document.getElementById('userName');
    const userAvatar = document.getElementById('userAvatar');
    const adminBadge = document.getElementById('adminBadge');
    if (state.currentUser) {
        userNameSpan.textContent = state.currentUser.name;
        if (state.currentUser.isAdmin === true || (state.currentUser.role && state.currentUser.role.toLowerCase() === 'admin')) {
            if (adminBadge) adminBadge.style.display = 'inline-block';
        } else {
            if (adminBadge) adminBadge.style.display = 'none';
        }
        if (userAvatar) {
            const initial = state.currentUser.name ? state.currentUser.name.charAt(0).toUpperCase() : 'U';
            if (state.currentUser.profilePhotoUrl) {
                userAvatar.innerHTML = `<img src="${state.currentUser.profilePhotoUrl}?t=${Date.now()}" style="width:100%;height:100%;border-radius:50%;object-fit:cover;" />`;
                userAvatar.style.background = 'transparent';
                userAvatar.style.padding = '0';
                userAvatar.style.overflow = 'hidden';
            } else {
                userAvatar.innerHTML = '';
                userAvatar.textContent = initial;
                userAvatar.style.background = 'linear-gradient(90deg,var(--primary-color), var(--secondary-color))';
                userAvatar.style.color = '#fff';
                userAvatar.style.fontWeight = '800';
                userAvatar.style.overflow = 'hidden';
            }
        }
    } else {
        userNameSpan.textContent = 'Account';
        if (userAvatar) {
            userAvatar.innerHTML = '<i class="far fa-user"></i>';
            userAvatar.style.background = 'rgba(255,255,255,0.02)';
            userAvatar.style.color = 'var(--primary-color)';
            userAvatar.style.fontWeight = '700';
        }
    }
}

// Edit profile — inline form inside the user modal
document.addEventListener('DOMContentLoaded', () => {
    // Open edit profile page
    document.getElementById('editProfileBtn')?.addEventListener('click', () => {
        window.location.href = '/Account/EditProfile';
    });

    // Cancel edit
    document.getElementById('cancelEditBtn')?.addEventListener('click', () => {
        document.getElementById('profileEditSection')?.classList.add('hidden');
        document.getElementById('profileViewSection')?.classList.remove('hidden');
    });

    // Save profile
    document.getElementById('saveProfileBtn')?.addEventListener('click', async () => {
        const saveBtn = document.getElementById('saveProfileBtn');
        const errorEl = document.getElementById('editProfileError');
        const nameVal = document.getElementById('editName')?.value.trim();
        const phoneVal = document.getElementById('editPhone')?.value.trim();
        const addrVal = document.getElementById('editAddress')?.value.trim();

        if (!nameVal) {
            if (errorEl) { errorEl.textContent = 'Name is required.'; errorEl.style.display = 'block'; }
            return;
        }
        if (errorEl) errorEl.style.display = 'none';
        saveBtn.disabled = true;
        const orig = saveBtn.textContent;
        saveBtn.textContent = 'Saving…';

        try {
            const res = await fetch('/api/users/me', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ name: nameVal, mobile: phoneVal || null, address: addrVal || null })
            });

            if (!res.ok) {
                const txt = await res.clone().text();
                let msg = 'Failed to save profile.';
                try { msg = JSON.parse(txt).error || msg; } catch { msg = txt || msg; }
                throw new Error(msg);
            }

            const updated = await res.json();
            // Update state
            state.currentUser = { ...state.currentUser, name: updated.name || nameVal, mobile: phoneVal, address: addrVal };
            saveToLocalStorage();

            // Update display
            document.getElementById('profileName').textContent = state.currentUser.name;
            const avatarLg = document.getElementById('profileAvatar');
            if (avatarLg) avatarLg.textContent = state.currentUser.name.charAt(0).toUpperCase();
            updateUserUI();

            // Return to view
            document.getElementById('profileEditSection')?.classList.add('hidden');
            document.getElementById('profileViewSection')?.classList.remove('hidden');
            showToast('Profile updated successfully', 'success');
        } catch (err) {
            if (errorEl) { errorEl.textContent = err.message; errorEl.style.display = 'block'; }
        } finally {
            saveBtn.disabled = false;
            saveBtn.textContent = orig;
        }
    });
});

// ===== PRODUCT DATA =====
async function loadProducts() {
    showLoading();
    try {
        // Try public products endpoint first
        const res = await fetch(`${apiBase}/api/products`);
        if (!res.ok) throw new Error(`Failed to load products: ${res.status}`);
        let products = await res.json();

        // Fallback: if public endpoint returned nothing, try admin endpoint (use credentials)
        if (!products || products.length === 0) {
            try {
                const adminRes = await fetch(`${apiBase}/api/admin/products`, { credentials: 'include' });
                if (adminRes.ok) {
                    const adminProds = await adminRes.json();
                    if (adminProds && adminProds.length > 0) products = adminProds;
                }
            } catch (e) {
                console.warn('Could not load admin products', e);
            }
        }

        // Normalize categories to lowercase for consistent filtering
        state.products = (products || []).map(p => ({
            ...p,
            category: (p.category || '').toString().toLowerCase(),
            imageUrl: p.imageUrl && p.imageUrl.trim() ? p.imageUrl : '/img/placeholder-product.svg',
            // normalize inStock field from API (handle both camelCase and PascalCase)
            inStock: !!(p.inStock ?? p.InStock)
        }));
        // show quick debug info (development)
        const dbg = document.getElementById('debugInfo');
        if (dbg) dbg.textContent = `products:${state.products.length}`;
    } catch (err) {
        console.error('Failed to load products from API', err);
        state.products = [];
        showToast('Could not load products from server', 'error');
    }

    state.filteredProducts = [...state.products];
    hideLoading();
    renderProducts();
}

// Ensure UI updates if products are reloaded or stock changes
async function refreshProductsAndUI() {
    await loadProducts();
    renderProducts();
}

function showLoading() {
    const s = document.getElementById('loadingSpinner');
    const c = document.getElementById('productsContainer');
    if (s) s.classList.remove('hidden');
    if (c) c.style.opacity = '0.5';
}

function hideLoading() {
    const s = document.getElementById('loadingSpinner');
    const c = document.getElementById('productsContainer');
    if (s) s.classList.add('hidden');
    if (c) c.style.opacity = '1';
}

// ===== FILTERING & SORTING =====
function applyFilters() {
    let filtered = [...state.products];

    // Category filter
    if (state.currentCategory !== 'all') {
        filtered = filtered.filter(p => p.category === state.currentCategory);
    }

    // Search filter
    if (state.filters.searchTerm) {
        const term = state.filters.searchTerm.toLowerCase();
        filtered = filtered.filter(p =>
            p.name.toLowerCase().includes(term) ||
            p.description.toLowerCase().includes(term)
        );
    }

    // Price filter
    if (state.filters.minPrice !== null) {
        filtered = filtered.filter(p => p.price >= state.filters.minPrice);
    }
    if (state.filters.maxPrice !== null) {
        filtered = filtered.filter(p => p.price <= state.filters.maxPrice);
    }

    // Rating filter
    if (state.filters.rating.length > 0) {
        const minRating = Math.min(...state.filters.rating);
        filtered = filtered.filter(p => p.rating >= minRating);
    }

    // Stock filter
    if (state.filters.inStock) {
        filtered = filtered.filter(p => p.inStock);
    }

    // Sorting
    filtered.sort((a, b) => {
        switch (state.filters.sortBy) {
            case 'price-low':
                return a.price - b.price;
            case 'price-high':
                return b.price - a.price;
            case 'rating':
                return b.rating - a.rating;
            case 'newest':
                return b.id - a.id;
            default:
                return b.reviews - a.reviews;
        }
    });

    state.filteredProducts = filtered;
    renderProducts();
}

// UI upgrade: keep modern filter chips and labels in sync without changing filter logic
function refreshFilterVisualState() {
    document.querySelectorAll('.filter-option').forEach(option => {
        const input = option.querySelector('input[type="checkbox"]');
        option.classList.toggle('active', !!input?.checked);
    });
}

function initializeFilterSidebarUI() {
    document.querySelectorAll('.filter-group[data-collapsible]').forEach(group => {
        const toggle = group.querySelector('.filter-group-toggle');
        if (!toggle) return;
        toggle.addEventListener('click', () => {
            const isOpen = group.classList.toggle('is-open');
            toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        });
    });

    refreshFilterVisualState();
}

function initializePriceRangeUI() {
    const slider = document.getElementById('priceRange');
    const minInput = document.getElementById('minPrice');
    const maxInput = document.getElementById('maxPrice');
    const valueLabel = document.getElementById('priceRangeValue');
    if (!slider || !maxInput || !valueLabel) return;

    const syncLabel = (val) => {
        const amount = Number(val || 0);
        valueLabel.textContent = `Up to ${money(amount)}`;
    };

    slider.addEventListener('input', () => {
        maxInput.value = slider.value;
        if (!minInput.value) minInput.value = 0;
        syncLabel(slider.value);
    });

    syncLabel(slider.value);
}

// ===== RENDERING FUNCTIONS =====
function renderProducts() {
    const container = document.getElementById('productsContainer');
    if (!container) return; // not on the shop listing page
    const noResults = document.getElementById('noResults');
    const resultsMeta = document.getElementById('resultsMeta');
    // Toggle list/grid layout class based on current view
    if (state.currentView === 'list') container.classList.add('list-view');
    else container.classList.remove('list-view');

    container.innerHTML = '';

    if (state.filteredProducts.length === 0) {
        noResults.classList.remove('hidden');
        if (resultsMeta) resultsMeta.textContent = '0 products found';
        return;
    }

    noResults.classList.add('hidden');
    if (resultsMeta) resultsMeta.textContent = `${state.filteredProducts.length} products found`;

    // Sync breadcrumb + page title with active category
    const catLabel = state.activeCategory && state.activeCategory !== 'all'
        ? state.activeCategory.charAt(0).toUpperCase() + state.activeCategory.slice(1)
        : 'All Products';
    const breadcrumb = document.getElementById('breadcrumbCategory');
    const shopTitle = document.getElementById('shopTitle');
    if (breadcrumb) breadcrumb.textContent = catLabel;
    if (shopTitle) shopTitle.textContent = catLabel === 'All Products' ? 'Solar Products' : catLabel;

    state.filteredProducts.forEach(product => {
        const isInWishlist = state.wishlist.some(id => id === product.id);
        const hasReviews = Number(product.reviews || 0) > 0;
        const ratingSummary = hasReviews
            ? `${Number(product.rating || 0).toFixed(1)} | ${product.reviews} reviews`
            : 'No reviews yet';
        const originalPriceValue = Number(product.originalPrice || 0);
        const currentPriceValue = Number(product.price || 0);
        const savingsAmount = originalPriceValue > currentPriceValue ? (originalPriceValue - currentPriceValue) : 0;
        // Calculate discount percentage client-side if server didn't provide it
        const discountPerc = (product.discount && Number(product.discount) > 0)
            ? Number(product.discount)
            : (product.originalPrice && Number(product.originalPrice) > Number(product.price))
                ? Math.round(((Number(product.originalPrice) - Number(product.price)) / Number(product.originalPrice)) * 100)
                : 0;

        const card = document.createElement('div');
        card.className = 'product-card';
        // store id on card for reliable access
        card.dataset.id = product.id;
        card.innerHTML = `
      <div class="product-badges">
        ${discountPerc > 0 ? `<span class="badge discount-badge">${discountPerc}% OFF</span>` : ''}
        ${!product.inStock ? `<span class="badge stock-badge">Out of Stock</span>` : ''}
      </div>
      <div class="product-image-wrapper">
        <button class="wishlist-icon ${isInWishlist ? 'active' : ''}" data-id="${product.id}">
          <i class="fa${isInWishlist ? 's' : 'r'} fa-heart"></i>
        </button>
        <img src="${product.imageUrl || 'https://via.placeholder.com/500?text=No+Image'}" alt="${product.name}" class="product-image" loading="lazy" onerror="this.onerror=null;this.src='https://via.placeholder.com/500?text=No+Image';" />
      </div>
      <div class="product-body">
        <div class="product-category">${product.category}</div>
        <h3 class="product-title">${product.name}</h3>
        <div class="product-rating ${hasReviews ? '' : 'no-reviews'}">
          ${hasReviews ? `<div class="stars">${generateStars(product.rating)}</div>` : ''}
          <span class="rating-count">${ratingSummary}</span>
        </div>
        <div class="product-price-row">
          <div class="price-main">
            <div class="price-line">
              <div class="product-price">${money(product.price)}</div>
              ${product.originalPrice ? `<div class="product-mrp"><span class="product-original-price"><span class="price-text">${money(product.originalPrice)}</span><span class="mrp-line" aria-hidden="true"></span></span></div>` : ''}
            </div>
            <div class="price-benefits">
              ${savingsAmount > 0 ? `<span class="save-amount">Save ${money(savingsAmount)}</span>` : ''}
              ${discountPerc > 0 ? `<span class="save-percent">${discountPerc}% OFF</span>` : ''}
            </div>
            <div class="price-note">Inclusive of all taxes</div>
          </div>
        </div>
        <div class="trust-signals">
          <span class="stock-indicator ${product.inStock ? 'in' : 'out'}">
            <i class="fas fa-circle"></i> ${product.inStock ? 'In stock' : 'Out of stock'}
          </span>
          <span class="delivery-text"><i class="fas fa-truck"></i> Free delivery in 3 days</span>
        </div>
        <div class="product-footer">
          <div class="cart-control" data-id="${product.id}" style="flex:1"></div>
          <button class="btn-quick-view btn-buy-now" data-id="${product.id}" title="Buy Now">
            <i class="fas fa-bolt"></i>
            <span>Buy Now</span>
          </button>
        </div>
      </div>
    `;

        container.appendChild(card);
        // Immediately set image src if available to ensure visibility
        const img = card.querySelector('.product-image');
        if (img && img.getAttribute('src') && img.getAttribute('src').trim()) {
            img.src = img.getAttribute('src');
        }
        // Make image and title clickable (navigate to product detail) â€” attach directly so parent handlers
        try {
            if (img) {
                img.style.cursor = 'pointer';
                img.addEventListener('click', (e) => {
                    e.stopPropagation();
                    window.location.href = `/Shop/Product/${product.id}`;
                });
            }
            const title = card.querySelector('.product-title');
            if (title) {
                title.style.cursor = 'pointer';
                title.addEventListener('click', (e) => { e.stopPropagation(); window.location.href = `/Shop/Product/${product.id}`; });
            }
        } catch (ee) { /* ignore attach errors */ }
        // render cart control for this card (Add to Cart or quantity controls)
        renderProductCardControl(product, card);

        // make whole card clickable (except interactive controls)
        card.addEventListener('click', (e) => {
            if (e.target.closest('button, a, input')) return;
            window.location.href = `/Shop/Product/${product.id}`;
        });
    });

    // Attach event listeners
    document.querySelectorAll('.wishlist-icon').forEach(btn => {
        btn.addEventListener('click', e => {
            e.stopPropagation();
            const id = parseInt(btn.getAttribute('data-id'));
            toggleWishlist(id);
        });
    });

    // individual product card cart controls are rendered per-card via renderProductCardControl()

    // Quick view buttons (exclude Buy Now which uses a different flow)
    document.querySelectorAll('.btn-quick-view:not(.btn-buy-now)').forEach(btn => {
        btn.addEventListener('click', e => {
            e.stopPropagation();
            const id = btn.getAttribute('data-id') || btn.closest('.product-card')?.dataset.id;
            if (id) {
                // Open full product detail page instead of modal quick-view
                navigateToProduct(id);
            }
        });
    });

    // Buy Now: go to checkout immediately. Only add 1 if the product is not already in cart.
    document.querySelectorAll('.btn-buy-now').forEach(btn => {
        btn.addEventListener('click', e => {
            e.stopPropagation();
            const id = btn.getAttribute('data-id') || btn.closest('.product-card')?.dataset.id;
            if (!id) return;
            btn.disabled = true;
            const already = state.cart.find(i => String(i.productId) === String(id));
            if (!already) {
                try { addToCart(id, 1); } catch (ex) { /* ignore */ }
            }
            setTimeout(() => {
                openCheckout();
                btn.disabled = false;
            }, 300);
        });
    });

    // Note: product card clicks navigate to product detail page (wired below).

    // Also attach cart item listeners (ensure they work after render)
    document.querySelectorAll('#cartItems .qty-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const action = btn.getAttribute('data-action');
            const idAttr = btn.getAttribute('data-id');
            const pidKey = String(idAttr);
            const item = state.cart.find(i => String(i.productId) === pidKey);
            if (!item) return;
            if (action === 'inc') updateCartQuantity(pidKey, item.quantity + 1);
            else updateCartQuantity(pidKey, item.quantity - 1);
        });
    });

    document.querySelectorAll('#cartItems .btn-remove').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const idAttr = btn.getAttribute('data-id');
            removeFromCart(String(idAttr));
        });
    });
}

// ===== CART MANAGEMENT =====
function findProduct(id) {
    // Coerce to number/string tolerant comparison because ids can come from localStorage as strings
    return state.products.find(p => {
        try {
            return String(p.id) === String(id);
        } catch {
            return p.id === id;
        }
    });
}

function addToCart(productId, quantity) {
    const product = findProduct(productId);
    if (!product) {
        showToast('Product not found', 'error');
        return;
    }

    const pidKey = String(productId);
    const existing = state.cart.find(i => String(i.productId) === pidKey);
    const q = Math.max(1, parseInt(quantity) || 1);

    if (existing) {
        existing.quantity += q;
    } else {
        // store productId as string to keep cart ids consistent
        state.cart.push({ productId: pidKey, quantity: q });
    }

    saveToLocalStorage();
    renderCart();

    if (state.currentUser && state.currentUser.email) {
        syncCartItemToServer(productId, state.cart.find(i => i.productId === productId)?.quantity || q);
    }

    showToast(`added to cart!`, 'success');
    //showMiniToast('Added to cart');
}

function removeFromCart(productId) {
    const pidKey = String(productId);
    state.cart = state.cart.filter(i => String(i.productId) !== pidKey);
    saveToLocalStorage();
    renderCart();

    // If cart is now empty, re-render product grid to ensure card controls reset
    try {
        if (!state.cart || state.cart.length === 0) renderProducts();
    } catch (e) { }

    if (state.currentUser && state.currentUser.email) {
        fetch(`${apiBase}/api/cart/${productId}`, { method: 'DELETE', credentials: 'include' }).catch(() => { });
    }

    showToast('Item successfully removed from cart', 'success');
}

function updateCartQuantity(productId, quantity) {
    const pidKey = String(productId);
    const item = state.cart.find(i => String(i.productId) === pidKey);
    if (!item) return;

    if (quantity <= 0) {
        removeFromCart(productId);
    } else {
        item.quantity = quantity;
        saveToLocalStorage();
        renderCart();

        if (state.currentUser && state.currentUser.email) {
            syncCartItemToServer(productId, quantity);
        }
    }
}

function syncCartItemToServer(productId, quantity) {
    fetch(`${apiBase}/api/cart`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ productId, quantity }),
        credentials: 'include'
    }).catch(() => { });
}

function cartTotal() {
    return state.cart.reduce((sum, item) => {
        const product = findProduct(item.productId);
        return sum + (product ? product.price * item.quantity : 0);
    }, 0);
}

function renderCart() {
    // Notify product-detail.js so its cart control stays in sync
    document.dispatchEvent(new CustomEvent('cartUpdated'));

    const cartItems = document.getElementById('cartItems');
    const cartCount = document.getElementById('cartCount');
    const emptyCart = document.getElementById('emptyCart');
    const cartSubtotal = document.getElementById('cartSubtotal');
    const cartTotalEl = document.getElementById('cartTotal');

    cartItems.innerHTML = '';

    const count = state.cart.reduce((sum, i) => sum + i.quantity, 0);
    cartCount.textContent = count;

    if (state.cart.length === 0) {
        emptyCart.classList.remove('hidden');
        cartItems.classList.add('hidden');
        // Ensure totals are reset when cart is empty (avoid stale cached totals)
        if (cartSubtotal) cartSubtotal.textContent = money(0);
        if (cartTotalEl) cartTotalEl.textContent = money(0);
        // clear persisted cart to avoid stale cached values across reloads
        try { localStorage.removeItem('cart'); } catch { }
        // ensure cart count displayed is zero
        if (cartCount) cartCount.textContent = '0';
        // Ensure product cards update to show Add to Cart when cart is empty
        try {
            document.querySelectorAll('.product-card').forEach(card => {
                const pid = card.dataset.id || card.querySelector('.cart-control')?.getAttribute('data-id');
                if (!pid) return;
                const prod = findProduct(pid);
                if (prod) renderProductCardControl(prod, card);
            });
        } catch (e) { /* ignore */ }

        return;
    }

    emptyCart.classList.add('hidden');
    cartItems.classList.remove('hidden');

    state.cart.forEach(item => {
        const product = findProduct(item.productId);
        if (!product) return;

        const li = document.createElement('li');
        li.className = 'cart-item';
        li.innerHTML = `
      <img src="${product.imageUrl || ''}" alt="${product.name}" class="cart-item-image" onerror="this.onerror=null;this.src='/img/placeholder-product.svg';this.style.padding='10px';" />
      <div class="cart-item-details">
        <h4>${product.name}</h4>
        <div class="cart-item-price">${money(product.price)}</div>
        <div class="cart-item-actions">
          <div class="qty-controls">
            <button type="button" class="qty-btn" data-action="dec" data-id="${product.id}">-</button>
            <span class="qty-value">${item.quantity}</span>
            <button type="button" class="qty-btn" data-action="inc" data-id="${product.id}">+</button>
          </div>
          <button type="button" class="btn-remove" data-id="${product.id}">
            <i class="fas fa-trash"></i>
          </button>
        </div>
      </div>
    `;
        cartItems.appendChild(li);
    });

    const total = cartTotal();
    cartSubtotal.textContent = money(total);
    if (cartTotalEl) cartTotalEl.textContent = money(total);

    // Attach listeners
    // Event delegation for quantity and remove buttons handled globally

    // Update product card controls to reflect current cart quantities
    try {
        document.querySelectorAll('.product-card').forEach(card => {
            const pid = card.dataset.id || card.querySelector('.cart-control')?.getAttribute('data-id');
            if (!pid) return;
            const prod = findProduct(pid);
            if (prod) renderProductCardControl(prod, card);
        });
    } catch (e) { /* ignore */ }
}

function toggleCart() {
    const panel = document.getElementById('cartPanel');
    const overlay = document.getElementById('overlay');

    panel.classList.toggle('open');
    overlay.classList.toggle('active');
}

// ===== WISHLIST MANAGEMENT =====
function toggleWishlist(productId) {
    const index = state.wishlist.indexOf(productId);
    const product = findProduct(productId);

    if (index > -1) {
        state.wishlist.splice(index, 1);
        showToast(`${product?.name || 'Item'} removed from wishlist`, 'success');
    } else {
        state.wishlist.push(productId);
        showToast(`${product?.name || 'Item'} added to wishlist!`, 'success');
    }

    // If user is logged in, persist to server
    if (state.currentUser && state.currentUser.email) {
        // try to call API
        const url = `${apiBase}/api/wishlist`;
        if (index > -1) {
            fetch(`${url}/${productId}`, { method: 'DELETE', credentials: 'include' }).catch(() => { });
        } else {
            fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(productId), credentials: 'include' }).catch(() => { });
        }
    }

    saveToLocalStorage();
    // Re-render products to update wishlist icons
    applyFilters();
    renderWishlist();
}

function renderWishlist() {
    const wishlistItems = document.getElementById('wishlistItems');
    const wishlistCount = document.getElementById('wishlistCount');
    const emptyWishlist = document.getElementById('emptyWishlist');

    wishlistCount.textContent = state.wishlist.length;
    wishlistItems.innerHTML = '';

    if (state.wishlist.length === 0) {
        emptyWishlist.classList.remove('hidden');
        wishlistItems.classList.add('hidden');
        return;
    }

    emptyWishlist.classList.add('hidden');
    wishlistItems.classList.remove('hidden');

    state.wishlist.forEach(productId => {
        const product = findProduct(productId);
        if (!product) return;

        const li = document.createElement('li');
        li.className = 'cart-item';
        li.innerHTML = `
      <img src="${product.imageUrl || ''}" alt="${product.name}" class="cart-item-image" onerror="this.onerror=null;this.src='/img/placeholder-product.svg';this.style.padding='10px';" />
      <div class="cart-item-details">
        <h4>${product.name}</h4>
        <div class="cart-item-price">${money(product.price)}</div>
        <div class="cart-item-actions">
          <button class="btn-add-cart" data-id="${product.id}" style="padding: 8px 15px; font-size: 13px;">
            <i class="fas fa-cart-plus"></i> Add to Cart
          </button>
          <button class="btn-remove" data-id="${product.id}">
            <i class="fas fa-times"></i>
          </button>
        </div>
      </div>
    `;
        wishlistItems.appendChild(li);
    });

    // Attach listeners
    wishlistItems.querySelectorAll('.btn-add-cart').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = parseInt(btn.getAttribute('data-id'));
            addToCart(id, 1);
        });
    });

    wishlistItems.querySelectorAll('.btn-remove').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = parseInt(btn.getAttribute('data-id'));
            toggleWishlist(id);
        });
    });
}

function toggleWishlistPanel() {
    const panel = document.getElementById('wishlistPanel');
    const overlay = document.getElementById('overlay');

    panel.classList.toggle('open');
    overlay.classList.toggle('active');
}

// ===== PRODUCT MODAL =====
function showProductModal(productId) {
    const product = findProduct(productId);
    if (!product) return;

    state.selectedProduct = product;

    const modal = document.getElementById('productModal');
    document.getElementById('modalProductImage').src = product.imageUrl;
    document.getElementById('modalProductName').textContent = product.name;
    document.getElementById('modalProductStars').innerHTML = generateStars(product.rating);
    document.getElementById('modalProductReviews').textContent = `${product.reviews} reviews`;
    document.getElementById('modalProductPrice').textContent = money(product.price);

    if (product.discount) {
        document.getElementById('modalProductDiscount').textContent = `Save ${product.discount}%`;
        document.getElementById('modalProductDiscount').style.display = 'block';
    } else {
        document.getElementById('modalProductDiscount').style.display = 'none';
    }

    // Allow structured JSON descriptions first, then fallback to legacy HTML/plain text
    try {
        const descEl = document.getElementById('modalProductDesc');
        const raw = product.description || '';

        const escapeHtml = (s) => String(s || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');

        // Structured payload from admin: { overview, features[], benefits[], specifications[{key,value}] }
        let structured = null;
        try {
            const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
            if (parsed && typeof parsed === 'object' && (parsed.overview || parsed.features || parsed.benefits || parsed.specifications)) {
                structured = {
                    overview: (parsed.overview || '').toString(),
                    features: Array.isArray(parsed.features) ? parsed.features.map(x => String(x)).filter(Boolean) : [],
                    benefits: Array.isArray(parsed.benefits) ? parsed.benefits.map(x => String(x)).filter(Boolean) : [],
                    specifications: Array.isArray(parsed.specifications)
                        ? parsed.specifications
                            .map(s => ({ key: (s?.key || '').toString(), value: (s?.value || '').toString() }))
                            .filter(s => s.key && s.value)
                        : []
                };
            }
        } catch { /* not json, continue legacy path */ }

        if (structured) {
            const featuresHtml = structured.features.length
                ? `<ul style="list-style:none; padding:0; margin:8px 0;">${structured.features.map(f => `<li style=\"margin:6px 0; display:flex; gap:8px; align-items:flex-start;\"><i class=\"fas fa-check-circle\" style=\"color:var(--primary-color); margin-top:3px;\"></i><span>${escapeHtml(f)}</span></li>`).join('')}</ul>`
                : '<div style="color:var(--muted); font-size:13px;">No feature highlights available.</div>';
            const benefitsHtml = structured.benefits.length
                ? `<ul style="list-style:none; padding:0; margin:8px 0;">${structured.benefits.map(b => `<li style=\"margin:6px 0; display:flex; gap:8px; align-items:flex-start;\"><i class=\"fas fa-bolt\" style=\"color:var(--secondary-color); margin-top:3px;\"></i><span>${escapeHtml(b)}</span></li>`).join('')}</ul>`
                : '<div style="color:var(--muted); font-size:13px;">No benefits listed.</div>';
            const specsHtml = structured.specifications.length
                ? `<div style="border:1px solid rgba(15,23,42,.08); border-radius:10px; overflow:hidden; margin-top:8px;"><table style="width:100%; border-collapse:collapse; font-size:13px;"><tbody>${structured.specifications.map(s => `<tr><th style=\"padding:8px 10px; text-align:left; background:#f8fafc; border-bottom:1px solid rgba(15,23,42,.06); width:35%;\">${escapeHtml(s.key)}</th><td style=\"padding:8px 10px; border-bottom:1px solid rgba(15,23,42,.06);\">${escapeHtml(s.value)}</td></tr>`).join('')}</tbody></table></div>`
                : '<div style="color:var(--muted); font-size:13px;">No technical specifications available.</div>';

            descEl.innerHTML = `
        <div style="display:grid; gap:10px;">
          <div><strong style="display:block; margin-bottom:4px; color:var(--dark-navy);">Overview</strong><div>${escapeHtml(structured.overview || 'Product information is being updated.')}</div></div>
          <div><strong style="display:block; margin-bottom:4px; color:var(--dark-navy);">Features</strong>${featuresHtml}</div>
          <div><strong style="display:block; margin-bottom:4px; color:var(--dark-navy);">Benefits</strong>${benefitsHtml}</div>
          <div><strong style="display:block; margin-bottom:4px; color:var(--dark-navy);">Specifications</strong>${specsHtml}</div>
        </div>
      `;
            return;
        }

        // Remove any script tags for safety
        // decode HTML entities so saved escaped full-doc HTML renders correctly
        const txt = document.createElement('textarea');
        txt.innerHTML = raw;
        let cleaned = txt.value.replace(/<script[\s\S]*?>[\s\S]*?<\/script>/gi, '');

        // If user saved a full HTML document, parse and extract styles + body
        if (/<!doctype|<html\b/i.test(cleaned)) {
            try {
                const parser = new DOMParser();
                const doc = parser.parseFromString(cleaned, 'text/html');
                // gather <style> contents from head
                const styles = Array.from(doc.querySelectorAll('style')).map(s => s.textContent).join('\n');
                const bodyHtml = doc.body ? doc.body.innerHTML : cleaned;
                // Put extracted styles scoped inside a wrapper so they apply to modal content
                const scoped = `<div class="product-desc-html">${bodyHtml}</div>`;
                descEl.innerHTML = (styles ? `<style>${styles}</style>` : '') + scoped;
            } catch (pe) {
                descEl.innerHTML = cleaned;
            }
        } else {
            // plain fragment html
            descEl.innerHTML = cleaned;
        }
    } catch (e) {
        // fallback to textContent if anything goes wrong
        document.getElementById('modalProductDesc').textContent = product.description;
    }

    const features = document.getElementById('modalProductFeatures');
    features.innerHTML = '';
    if (product.features) {
        product.features.forEach(feature => {
            const li = document.createElement('li');
            li.textContent = feature;
            features.appendChild(li);
        });
    }

    const isInWishlist = state.wishlist.includes(product.id);
    const wishlistBtn = document.getElementById('modalAddToWishlist');
    wishlistBtn.className = `btn-wishlist ${isInWishlist ? 'active' : ''}`;
    wishlistBtn.innerHTML = `<i class="fa${isInWishlist ? 's' : 'r'} fa-heart"></i>`;

    modal.classList.add('open');
    document.getElementById('overlay').classList.add('active');
}

function closeProductModal() {
    document.getElementById('productModal').classList.remove('open');
    document.getElementById('overlay').classList.remove('active');
}

// ===== CHECKOUT =====
function openCheckout() {
    if (state.cart.length === 0) {
        showToast('Your cart is empty', 'error');
        return;
    }
    // Enrich cart items with product data from state.products before navigating,
    // because state.cart only stores { productId, quantity }.
    try {
        const snap = state.cart.map(i => {
            const p = state.products.find(pr => String(pr.id) === String(i.productId) || String(pr.productId) === String(i.productId));
            return {
                productId: i.productId,
                name: p?.name || i.name || 'Product',
                price: p?.price || i.price || 0,
                quantity: i.quantity || 1,
                imageUrl: p?.imageUrl || i.imageUrl || '',
                category: p?.category || i.category || ''
            };
        });
        sessionStorage.setItem('DNACheckoutCart', JSON.stringify(snap));
    } catch (e) { /* ignore */ }
    window.location.href = '/Shop/Checkout';
}

function closeCheckout() { /* no-op — checkout is now a dedicated page */ }

// ===== USER AUTH =====
function openUserModal() {
    const modal = document.getElementById('userModal');
    const overlay = document.getElementById('overlay');

    // If state was cleared by a transient error, restore from localStorage
    if (!state.currentUser) {
        try {
            const stored = localStorage.getItem('currentUser');
            if (stored) state.currentUser = JSON.parse(stored);
        } catch { /* ignore */ }
    }

    // show profile if logged in
    if (state.currentUser) {
        document.getElementById('userProfile').classList.remove('hidden');
        document.getElementById('authForms').classList.add('hidden');
        document.getElementById('profileName').textContent = state.currentUser.name || '';
        document.getElementById('profileEmail').textContent = state.currentUser.email || '';
        // Also update the large avatar initial
        const avatarLg = document.getElementById('profileAvatar');
        if (avatarLg && state.currentUser.name) avatarLg.textContent = state.currentUser.name.charAt(0).toUpperCase();
        // Make sure edit form is hidden on open
        const editSection = document.getElementById('profileEditSection');
        const viewSection = document.getElementById('profileViewSection');
        if (editSection) editSection.classList.add('hidden');
        if (viewSection) viewSection.classList.remove('hidden');
    } else {
        document.getElementById('userProfile').classList.add('hidden');
        document.getElementById('authForms').classList.remove('hidden');
        switchAuthTab('login');
    }
    modal.classList.add('open');
    overlay.classList.add('active');
}

function closeUserModal() {
    document.getElementById('userModal').classList.remove('open');
    document.getElementById('overlay').classList.remove('active');
}

function switchAuthTab(tab) {
    const login = document.getElementById('loginForm');
    const register = document.getElementById('registerForm');
    const sLogin = document.getElementById('showLogin');
    const sReg = document.getElementById('showRegister');
    if (tab === 'login') {
        _resetOtpUi();
        login.classList.remove('hidden');
        register.classList.add('hidden');
        sLogin.classList.add('active');
        sReg.classList.remove('active');
    } else {
        login.classList.add('hidden');
        register.classList.remove('hidden');
        sLogin.classList.remove('active');
        sReg.classList.add('active');
    }
}

// ── OTP registration state ──────────────────────────────────────
let _regOtpSent = false;
let _regOtpTimerInterval = null;
let _regPendingName = '', _regPendingEmail = '', _regPendingPassword = '';

function _startOtpCountdown() {
    clearInterval(_regOtpTimerInterval);
    const timerEl = document.getElementById('otpTimer');
    const resendBtn = document.getElementById('resendOtpBtn');
    let secs = 10 * 60;
    if (timerEl) timerEl.textContent = '10:00';
    if (resendBtn) resendBtn.style.display = 'none';
    _regOtpTimerInterval = setInterval(() => {
        secs--;
        if (timerEl) {
            const m = String(Math.floor(secs / 60)).padStart(2, '0');
            const s = String(secs % 60).padStart(2, '0');
            timerEl.textContent = `${m}:${s}`;
        }
        if (secs <= 0) {
            clearInterval(_regOtpTimerInterval);
            if (timerEl) timerEl.textContent = '00:00';
            if (resendBtn) resendBtn.style.display = 'inline-block';
        }
    }, 1000);
}

function _resetOtpUi() {
    _regOtpSent = false;
    clearInterval(_regOtpTimerInterval);
    _regPendingName = ''; _regPendingEmail = ''; _regPendingPassword = '';
    const otpSection = document.getElementById('otpSection');
    const regStep1 = document.getElementById('regStep1');
    const submitBtn = document.getElementById('registerSubmitBtn');
    const otpInput = document.getElementById('otpInput');
    if (otpSection) otpSection.style.display = 'none';
    if (regStep1) regStep1.style.display = '';
    if (submitBtn) submitBtn.innerHTML = 'Send Verification Code <i class="fas fa-envelope"></i>';
    if (otpInput) otpInput.value = '';
}

async function sendRegistrationOtp(name, email, password) {
    if (!name || !email || !password) {
        showToast('Please fill all fields', 'error');
        return;
    }
    const submitBtn = document.getElementById('registerSubmitBtn');
    if (submitBtn) { submitBtn.disabled = true; submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Sending...'; }
    try {
        const res = await fetch(`${apiBase}/api/users/send-registration-otp`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email }),
            credentials: 'include'
        });
        if (!res.ok) {
            let msg = 'Could not send OTP';
            try { const j = await res.json(); msg = j.error || msg; } catch { }
            throw new Error(msg);
        }
        _regPendingName = name; _regPendingEmail = email; _regPendingPassword = password;
        _regOtpSent = true;
        const regStep1 = document.getElementById('regStep1');
        const otpSection = document.getElementById('otpSection');
        if (regStep1) regStep1.style.display = 'none';
        if (otpSection) otpSection.style.display = '';
        if (submitBtn) submitBtn.innerHTML = 'Create Account <i class="fas fa-user-plus"></i>';
        _startOtpCountdown();
        showToast(`OTP sent to ${email}`, 'success');
    } catch (err) {
        showToast(err.message || 'Failed to send OTP', 'error');
    } finally {
        if (submitBtn) submitBtn.disabled = false;
    }
}

async function registerUser(name, email, password) {
    if (!name || !email || !password) {
        showToast('Please fill all fields', 'error');
        return;
    }

    const otp = document.getElementById('otpInput')?.value?.trim();
    if (!otp) { showToast('Please enter the OTP', 'error'); return; }
    const submitBtn = document.getElementById('registerSubmitBtn');
    if (submitBtn) { submitBtn.disabled = true; submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Creating...'; }
    try {
        const res = await fetch(`${apiBase}/api/users/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, email, password, otp }),
            credentials: 'include'
        });

        if (!res.ok) {
            let msg = 'Failed to register';
            try {
                const txt = await res.clone().text();
                try { const j = JSON.parse(txt); msg = j.error || txt || msg; } catch { msg = txt || msg; }
            } catch { /* keep default */ }
            throw new Error(msg);
        }

        let user;
        try { user = await res.json(); } catch { throw new Error('Invalid server response during registration'); }
        state.currentUser = { id: user.id, name: user.name, email: user.email, isAdmin: false };
        saveToLocalStorage();
        refreshAuthUI();
        _resetOtpUi();
        showToast('Account created successfully!', 'success');
        openUserModal();
    } catch (err) {
        console.error(err);
        showToast(err.message || 'Registration failed', 'error');
    } finally {
        if (submitBtn) { submitBtn.disabled = false; submitBtn.innerHTML = 'Create Account <i class="fas fa-user-plus"></i>'; }
    }
}

async function loginUser(email, password) {
    if (!email || !password) {
        showToast('Please provide email and password', 'error');
        return;
    }

    try {
        const res = await fetch(`${apiBase}/api/users/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password }),
            credentials: 'include'
        });

        if (!res.ok) {
            let msg = 'Login failed';
            try {
                const txt = await res.clone().text();
                try { const j = JSON.parse(txt); msg = j.error || txt || msg; } catch { msg = txt || msg; }
            } catch { /* keep default */ }
            throw new Error(msg);
        }

        let user;
        try { user = await res.json(); } catch { throw new Error('Invalid server response during login'); }
        state.currentUser = { id: user.id, name: user.name, email: user.email, isAdmin: false };
        saveToLocalStorage();
        refreshAuthUI();

        // Immediately re-read current user/admin state from server so Admin button
        // appears right away for admin users.
        await loadCurrentUser();

        showToast('Logged in', 'success');
        openUserModal();
    } catch (err) {
        console.error(err);
        showToast(err.message || 'Login failed', 'error');
    }
}

async function logoutUser() {
    await logoutEverything();
    saveToLocalStorage();
    refreshAuthUI();
    showToast('Logged out', 'success');
}

// placeOrder is now handled by Views/Shop/Checkout.cshtml

// ===== EVENT LISTENERS =====
function initializeEventListeners() {
    // Search
    const searchInput = document.getElementById('searchInput');
    let searchTimeout;
    searchInput.addEventListener('input', e => {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => {
            state.filters.searchTerm = e.target.value;
            applyFilters();
        }, 300);
    });

    // Category buttons
    // Category buttons are generated dynamically by loadCategories()
    document.getElementById('categoryContainer')?.addEventListener('click', e => {
        const btn = e.target.closest('.cat-btn');
        if (!btn) return;
        document.querySelectorAll('.cat-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        state.currentCategory = btn.getAttribute('data-category');
        applyFilters();
    });

    // Price filter
    document.getElementById('applyPrice')?.addEventListener('click', () => {
        const min = parseFloat(document.getElementById('minPrice').value) || null;
        const max = parseFloat(document.getElementById('maxPrice').value) || null;
        state.filters.minPrice = min;
        state.filters.maxPrice = max;
        applyFilters();
    });

    // Rating filter
    document.querySelectorAll('input[name="rating"]').forEach(checkbox => {
        checkbox.addEventListener('change', () => {
            state.filters.rating = Array.from(document.querySelectorAll('input[name="rating"]:checked'))
                .map(cb => parseFloat(cb.value));
            refreshFilterVisualState();
            applyFilters();
        });
    });

    // Stock filter
    document.querySelector('input[name="stock"]')?.addEventListener('change', e => {
        state.filters.inStock = e.target.checked;
        refreshFilterVisualState();
        applyFilters();
    });

    // Sort
    document.getElementById('sortBy')?.addEventListener('change', e => {
        state.filters.sortBy = e.target.value;
        applyFilters();
    });

    // Clear filters
    document.getElementById('clearFilters')?.addEventListener('click', () => {
        state.filters = {
            searchTerm: '',
            minPrice: null,
            maxPrice: null,
            rating: [],
            inStock: false,
            sortBy: 'popularity'
        };
        document.getElementById('searchInput').value = '';
        document.getElementById('minPrice').value = '';
        document.getElementById('maxPrice').value = '';
        const priceRange = document.getElementById('priceRange');
        if (priceRange) {
            priceRange.value = '0';
            const valueLabel = document.getElementById('priceRangeValue');
            if (valueLabel) valueLabel.textContent = `Up to ${money(0)}`;
        }
        document.querySelectorAll('input[type="checkbox"]').forEach(cb => cb.checked = false);
        document.getElementById('sortBy').value = 'popularity';
        refreshFilterVisualState();
        applyFilters();
    });

    // View toggle
    // View toggle â€” ensure buttons toggle state and re-render
    document.querySelectorAll('.view-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.view-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            state.currentView = btn.getAttribute('data-view');
            // apply layout changes immediately
            const container = document.getElementById('productsContainer');
            if (container) {
                if (state.currentView === 'list') container.classList.add('list-view');
                else container.classList.remove('list-view');
            }
        });
    });

    // Expose quick toggle for inline onclick fallback in HTML
    window.toggleView = function (v) {
        try {
            state.currentView = v === 'list' ? 'list' : 'grid';
            document.querySelectorAll('.view-btn').forEach(b => b.classList.toggle('active', b.getAttribute('data-view') === state.currentView));
            const container = document.getElementById('productsContainer');
            if (container) {
                if (state.currentView === 'list') container.classList.add('list-view');
                else container.classList.remove('list-view');
            }
        } catch (e) { /* ignore */ }
    };

    // Cart
    document.getElementById('closeCart')?.addEventListener('click', toggleCart);
    document.getElementById('closeCartBtn')?.addEventListener('click', toggleCart);
    document.getElementById('checkoutBtn')?.addEventListener('click', openCheckout);

    // Delegated product controls: handle Add to Cart and card qty +/- reliably
    const productsContainer = document.getElementById('productsContainer');
    if (productsContainer && !productsContainer._cartDelegated) {
        productsContainer.addEventListener('click', (e) => {
            const addBtn = e.target.closest('.btn-add-cart');
            if (addBtn) {
                e.stopPropagation();
                const pid = addBtn.getAttribute('data-id') || addBtn.closest('.product-card')?.dataset.id;
                if (pid) addToCart(pid, 1);
                return;
            }

            const decr = e.target.closest('.qty-decr');
            if (decr) {
                e.stopPropagation();
                const card = decr.closest('.product-card');
                const pid = card?.dataset.id;
                if (pid) {
                    const cur = state.cart.find(i => String(i.productId) === String(pid));
                    const curQty = cur ? (cur.quantity || 0) : 0;
                    updateCartQuantity(pid, curQty - 1);
                }
                return;
            }

            const incr = e.target.closest('.qty-incr');
            if (incr) {
                e.stopPropagation();
                const card = incr.closest('.product-card');
                const pid = card?.dataset.id;
                if (pid) {
                    const cur = state.cart.find(i => String(i.productId) === String(pid));
                    const curQty = cur ? (cur.quantity || 0) : 0;
                    updateCartQuantity(pid, curQty + 1);
                }
                return;
            }
        });
        productsContainer._cartDelegated = true;
    }

    // Wishlist
    document.getElementById('closeWishlist')?.addEventListener('click', toggleWishlistPanel);

    // User / Auth
    document.getElementById('closeUserModal')?.addEventListener('click', closeUserModal);
    document.getElementById('showLogin')?.addEventListener('click', () => switchAuthTab('login'));
    document.getElementById('showRegister')?.addEventListener('click', () => switchAuthTab('register'));
    document.getElementById('loginForm')?.addEventListener('submit', e => {
        e.preventDefault();
        const fd = new FormData(e.target);
        loginUser(fd.get('email'), fd.get('password'));
    });
    document.getElementById('registerForm')?.addEventListener('submit', e => {
        e.preventDefault();
        const fd = new FormData(e.target);
        if (!_regOtpSent) {
            sendRegistrationOtp(fd.get('name'), fd.get('email'), fd.get('password'));
        } else {
            registerUser(_regPendingName, _regPendingEmail, _regPendingPassword);
        }
    });
    document.getElementById('resendOtpBtn')?.addEventListener('click', () => {
        sendRegistrationOtp(_regPendingName, _regPendingEmail, _regPendingPassword);
    });
    document.getElementById('logoutBtn')?.addEventListener('click', async () => {
        await logoutUser();
        closeUserModal();
    });

    // Product modal
    document.getElementById('closeProductModal')?.addEventListener('click', closeProductModal);
    document.getElementById('modalDecQty')?.addEventListener('click', () => {
        const input = document.getElementById('modalQty');
        input.value = Math.max(1, parseInt(input.value) - 1);
    });
    document.getElementById('modalIncQty')?.addEventListener('click', () => {
        const input = document.getElementById('modalQty');
        input.value = parseInt(input.value) + 1;
    });
    document.getElementById('modalAddToCart')?.addEventListener('click', () => {
        const qty = parseInt(document.getElementById('modalQty').value);
        addToCart(state.selectedProduct.id, qty);
    });
    document.getElementById('modalAddToWishlist')?.addEventListener('click', () => {
        toggleWishlist(state.selectedProduct.id);
        const isInWishlist = state.wishlist.includes(state.selectedProduct.id);
        const btn = document.getElementById('modalAddToWishlist');
        btn.className = `btn-wishlist ${isInWishlist ? 'active' : ''}`;
        btn.innerHTML = `<i class="fa${isInWishlist ? 's' : 'r'} fa-heart"></i>`;
    });

    // Checkout — handled by dedicated /Shop/Checkout page

    // Overlay — close whichever panel/modal is currently open (don't toggle blindly)
    document.getElementById('overlay')?.addEventListener('click', () => {
        if (document.getElementById('cartPanel')?.classList.contains('open')) toggleCart();
        if (document.getElementById('wishlistPanel')?.classList.contains('open')) toggleWishlistPanel();
        if (document.getElementById('productModal')?.classList.contains('open')) closeProductModal();
        // checkout is now a dedicated page — no modal to close
        if (document.getElementById('userModal')?.classList.contains('open')) closeUserModal();
    });

    // Global delegation for cart qty and remove buttons inside cart items
    const cartItemsEl = document.getElementById('cartItems');
    if (cartItemsEl) {
        cartItemsEl.addEventListener('click', (e) => {
            const btn = e.target.closest('.qty-btn, .btn-remove');
            if (!btn) return;
            e.stopPropagation();
            const id = String(btn.getAttribute('data-id'));
            if (btn.classList.contains('qty-btn')) {
                const action = btn.getAttribute('data-action');
                const item = state.cart.find(i => String(i.productId) === id);
                if (!item) return;
                if (action === 'inc') updateCartQuantity(id, item.quantity + 1);
                else updateCartQuantity(id, item.quantity - 1);
            } else if (btn.classList.contains('btn-remove')) {
                removeFromCart(id);
            }
        });
    }
}

async function loadCategories() {
    try {
        const res = await fetch(`${apiBase}/api/categories`);
        console.debug('categories fetch status', res.status);
        if (!res.ok) {
            console.warn('categories endpoint returned', res.status);
            return;
        }
        const cats = await res.json();
        const container = document.getElementById('categoryContainer');
        if (!container) return;
        if (cats && cats.length > 0) {
            container.innerHTML = '<button class="cat-btn active" data-category="all"><i class="fas fa-th"></i> All Products</button>' +
                cats.map(c => ` <button class="cat-btn" data-category="${c.name.toLowerCase()}"><i class="fas fa-tag"></i> ${c.name}</button>`).join('');
        } else {
            // derive categories from products if API returned none
            const unique = Array.from(new Set(state.products.map(p => (p.category || '').toString().toLowerCase()).filter(Boolean)));
            container.innerHTML = '<button class="cat-btn active" data-category="all"><i class="fas fa-th"></i> All Products</button>' +
                unique.map(name => ` <button class="cat-btn" data-category="${name}"><i class="fas fa-tag"></i> ${name}</button>`).join('');
            console.debug('derived categories from products', unique);
        }
        setDebug(`categories:${(cats && cats.length) || 0} products:${state.products.length}`);
    } catch (e) {
        console.warn('Failed to load categories', e);
    }
}

// ===== INITIALIZATION =====
async function init() {
    loadFromLocalStorage();
    // Ensure we refresh current user from server on startup so UI (Admin button)
    // reflects server-side role state rather than any stale localStorage copy.
    await loadCurrentUser();
    // Ensure any leftover modal/overlay state from previous sessions is cleared
    try {
        const ov = document.getElementById('overlay'); if (ov) ov.classList.remove('active');
        const pm = document.getElementById('productModal'); if (pm) pm.classList.remove('open');
        const wp = document.getElementById('wishlistPanel'); if (wp) wp.classList.remove('open');
        const cp = document.getElementById('cartPanel'); if (cp) cp.classList.remove('open');
        const um = document.getElementById('userModal'); if (um) um.classList.remove('open');
        // checkout is now a dedicated page
    } catch (e) { /* ignore */ }
    await loadProducts();
    await loadCategories();
    initializeEventListeners();
    initializeFilterSidebarUI();
    initializePriceRangeUI();
    renderCart();
    renderWishlist();
    refreshAuthUI();
    initUIEffects();
    initViewToggle();
}

// Start the app
window.addEventListener('DOMContentLoaded', () => { init().catch(e => console.error(e)); });

// Listen for product updates from admin tab and refresh product list
window.addEventListener('storage', (e) => {
    if (!e) return;
    if (e.key === 'product-updated') {
        // small delay to allow server commit
        setTimeout(() => { refreshProductsAndUI().catch(() => { }); }, 300);
    }
});

// UI Effects: reveal on scroll, lazy image reveal
function initViewToggle() {
    const viewBtns = document.querySelectorAll('.view-btn');
    const container = document.getElementById('productsContainer');
    if (!viewBtns.length || !container) return;

    viewBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            viewBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            if (btn.getAttribute('data-view') === 'list') {
                container.classList.add('list-view');
            } else {
                container.classList.remove('list-view');
            }
        });
    });

    // Ensure initial state follows the active button (or default state)
    const activeBtn = document.querySelector('.view-btn.active') || viewBtns[0];
    if (activeBtn) {
        const view = activeBtn.getAttribute('data-view') || 'grid';
        state.currentView = view;
        if (view === 'list') container.classList.add('list-view');
        else container.classList.remove('list-view');
    }
    // apply layout immediately in case products already rendered
    try { renderProducts(); } catch (e) { /* ignore */ }
}

function initUIEffects() {
    const observer = new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('reveal');
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.12 });

    // Observe product cards after render
    const watchCards = () => document.querySelectorAll('.product-card:not(.reveal)').forEach(c => observer.observe(c));
    // Lazy load images using native loading="lazy"; keep simple observer to add reveal class

    // hook into renderProducts by polling for container
    const container = document.getElementById('productsContainer');
    if (container) {
        const mo = new MutationObserver(() => {
            // Only trigger reveal animations; do not clear image src (browser handles lazy loading)
            watchCards();
        });
        mo.observe(container, { childList: true, subtree: true });
    }
}

