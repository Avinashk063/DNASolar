// product-detail.js

const PLACEHOLDER = `data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='120' viewBox='0 0 120 120'%3E%3Crect width='120' height='120' fill='%23f1f5f9'/%3E%3Ctext x='50%25' y='54%25' dominant-baseline='middle' text-anchor='middle' font-size='40' fill='%23cbd5e1'%3E%F0%9F%93%A6%3C/text%3E%3C/svg%3E`;
const fmt = n => '₹' + Number(n).toLocaleString('en-IN', { minimumFractionDigits: 0 });

let _currentProduct = null;
let _selectedRating = 0;
let _activeFilter = 0; // 0 = all
let _allReviews = [];
let _reviewImageBase64 = null;

// Re-render the product page cart control whenever shop.js updates the cart
document.addEventListener('cartUpdated', renderPdCartControl);
let _reviewVideoBase64 = null;

/* ── Helpers ── */
function starsHtml(rating, cls = 'stars') {
    let h = `<span class="${cls}">`;
    for (let i = 1; i <= 5; i++)
        h += `<i class="${i <= rating ? 'fas lit' : 'far'} fa-star"></i>`;
    return h + '</span>';
}

function timeAgo(dateStr) {
    const diff = Date.now() - new Date(dateStr).getTime();
    const d = Math.floor(diff / 86400000);
    if (d < 1) return 'Today';
    if (d < 7) return `${d} day${d > 1 ? 's' : ''} ago`;
    if (d < 30) return `${Math.floor(d / 7)} week${Math.floor(d / 7) > 1 ? 's' : ''} ago`;
    if (d < 365) return `${Math.floor(d / 30)} month${Math.floor(d / 30) > 1 ? 's' : ''} ago`;
    return `${Math.floor(d / 365)} year${Math.floor(d / 365) > 1 ? 's' : ''} ago`;
}

/* ── Load & render product ── */
async function loadProduct() {
    const id = (typeof CURRENT_PRODUCT_ID !== 'undefined' ? CURRENT_PRODUCT_ID : 0)
        || new URLSearchParams(location.search).get('id');
    const out = document.getElementById('productContainer');

    if (!id || id == 0) {
        out.innerHTML = `<div style="padding:80px;text-align:center;"><i class="fas fa-box-open" style="font-size:3rem;color:#cbd5e1;display:block;margin-bottom:1rem;"></i><h3 style="color:#334155;">Product not found</h3></div>`;
        return;
    }

    try {
        const [pRes, imgsRes] = await Promise.all([
            fetch(`/api/products/${id}`),
            fetch(`/api/productimages/product/${id}`)
        ]);
        if (!pRes.ok) throw new Error('Product not found');
        const product = await pRes.json();
        const gallery = imgsRes.ok ? await imgsRes.json() : [];
        _currentProduct = product;
        // Ensure this product is in shop.js state.products so addToCart can find it
        if (typeof state !== 'undefined' && Array.isArray(state.products)) {
            const already = state.products.find(p => String(p.id) === String(product.id));
            if (!already) state.products.push(product);
        }
        renderProduct(product, gallery);
        // Initial cart control render (after product HTML is injected)
        setTimeout(renderPdCartControl, 0);
        loadReviews(id);
    } catch (err) {
        out.innerHTML = `<div style="padding:80px;text-align:center;color:#b91c1c;">${err.message}</div>`;
    }
}

function renderProduct(p, gallery) {
    const out = document.getElementById('productContainer');
    const images = gallery.length ? gallery : [{ imageUrl: p.imageUrl || PLACEHOLDER }];
    const mainImg = images[0].imageUrl || PLACEHOLDER;

    // breadcrumb
    const bc = document.getElementById('breadcrumb-category');
    const bp = document.getElementById('breadcrumb-product');
    if (bc) bc.textContent = (p.category || 'Products').replace(/\b\w/g, c => c.toUpperCase());
    if (bp) bp.textContent = p.name;

    document.title = `${p.name} — DNA Softech`;

    const discount = p.discount || (p.originalPrice > 0 && p.price < p.originalPrice
        ? Math.round((1 - p.price / p.originalPrice) * 100) : 0);

    out.innerHTML = `
    <div class="pd-shell">
        <!-- Gallery -->
        <div class="gallery-card">
            <div class="image-stage">
                <img id="mainImg" class="main-image"
                     src="${mainImg}"
                     onerror="this.onerror=null;this.src='${PLACEHOLDER}'"
                     onclick="openLightbox(this.src)" alt="${p.name}" />
            </div>
            <div class="thumb-strip" id="thumbs">
                ${images.map((img, i) => `
                <div class="thumb ${i === 0 ? 'active' : ''}" onclick="switchImage('${img.imageUrl || PLACEHOLDER}', this)">
                    <img src="${img.imageUrl || PLACEHOLDER}" onerror="this.onerror=null;this.src='${PLACEHOLDER}'" alt="" />
                </div>`).join('')}
            </div>
        </div>

        <!-- Info -->
        <div class="info-panel">
            <!-- Main details card -->
            <div class="pd-card">
                <span class="category-tag">${p.category || 'Product'}</span>
                <h1 class="product-title">${p.name}</h1>

                <div class="rating-row">
                    ${starsHtml(Math.round(p.rating || 0))}
                    <span class="review-count" id="ratingLabel">${p.rating ? p.rating.toFixed(1) : 'No'} · ${p.reviews || 0} review${(p.reviews || 0) !== 1 ? 's' : ''}</span>
                </div>

                <!-- Price -->
                <div class="price-block" style="margin-top:.9rem;">
                    <span class="price-now">${fmt(p.price)}</span>
                    ${p.originalPrice > p.price ? `<span class="price-was">${fmt(p.originalPrice)}</span>` : ''}
                    ${discount > 0 ? `<span class="discount-pill">${discount}% OFF</span>` : ''}
                </div>
                <div class="tax-note">Inclusive of all taxes &amp; charges</div>

                <!-- Stock -->
                <div class="stock-row" style="margin-top:.9rem;">
                    <span class="dot ${p.inStock !== false ? 'green' : 'red'}"></span>
                    <span style="color:${p.inStock !== false ? '#15803d' : '#b91c1c'};">
                        ${p.inStock !== false ? 'In Stock' : 'Out of Stock'}
                    </span>
                    <span style="color:#94a3b8;font-size:.75rem;">· Free delivery in 3 days</span>
                </div>

                <!-- Cart control (Add to Cart  ↔  qty picker) -->
                <div class="purchase-row" style="margin-top:1.1rem;">
                    <div id="pdCartControl" style="display:flex;align-items:center;gap:10px;flex:1"></div>
                </div>

                <!-- Delivery badges -->
                <div class="delivery-badges" style="margin-top:1rem;">
                    <span class="d-badge"><i class="fas fa-truck"></i> Free Delivery</span>
                    <span class="d-badge"><i class="fas fa-rotate-left"></i> Easy Returns</span>
                    <span class="d-badge"><i class="fas fa-shield-halved"></i> Secure Payment</span>
                </div>
            </div>

            <!-- Description card -->
            ${p.description ? `
            <div class="pd-card">
                <div style="font-size:.78rem;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:#94a3b8;margin-bottom:.65rem;">Description</div>
                <p class="desc-text">${p.description}</p>
            </div>` : ''}
        </div>
    </div>

    <!-- Reviews section injected below by loadReviews() -->
    <div id="reviewsSection" class="reviews-section"></div>`;
}

/* ── Reviews ── */
async function loadReviews(productId) {
    const section = document.getElementById('reviewsSection');
    if (!section) return;

    let reviews = [];
    try {
        const res = await fetch(`/api/products/${productId}/reviews`);
        if (res.ok) reviews = await res.json();
    } catch { }

    renderReviews(productId, reviews);
}

function renderReviews(productId, reviews) {
    const section = document.getElementById('reviewsSection');
    if (!section) return;

    _allReviews = reviews;
    _activeFilter = 0;
    _reviewImageBase64 = null;
    _reviewVideoBase64 = null;

    // Compute stats
    const count = reviews.length;
    const avg = count ? (reviews.reduce((s, r) => s + r.rating, 0) / count) : 0;
    const dist = [5, 4, 3, 2, 1].map(star => ({
        star,
        count: reviews.filter(r => r.rating === star).length
    }));

    // Resolve logged-in user name
    let loggedInName = '';
    try {
        const cu = (typeof state !== 'undefined' && state.currentUser)
            || JSON.parse(localStorage.getItem('currentUser') || 'null');
        loggedInName = cu?.name || '';
    } catch { }

    section.innerHTML = `
    <h2 class="section-title"><i class="fas fa-star"></i> Customer Reviews</h2>
    <div class="reviews-grid">

        <!-- Summary -->
        <div class="rating-summary">
            <div class="avg-score">${count ? avg.toFixed(1) : '—'}</div>
            <div class="avg-stars">
                ${[1, 2, 3, 4, 5].map(i => `<i class="fa${i <= Math.round(avg) ? 's' : 'r'} fa-star${i <= Math.round(avg) ? ' lit' : ''}"></i>`).join('')}
            </div>
            <div class="avg-count">${count} review${count !== 1 ? 's' : ''}</div>
            <div class="bar-rows">
                ${dist.map(d => `
                <div class="bar-row" style="cursor:pointer;" onclick="applyFilter(${d.star})">
                    <span>${d.star}</span>
                    <div class="bar-track"><div class="bar-fill" style="width:${count ? Math.round(d.count / count * 100) : 0}%"></div></div>
                    <span>${d.count}</span>
                </div>`).join('')}
            </div>
        </div>

        <!-- Right: form + list -->
        <div class="reviews-right">

            <!-- Write a review -->
            <div class="write-review-card">
                <div class="write-title"><i class="fas fa-pen" style="color:#4f46e5;margin-right:.4rem;"></i>Write a Review</div>
                ${loggedInName ? `<div class="rv-reviewer-name"><i class="fas fa-user-circle"></i> Posting as <strong>${loggedInName}</strong></div>` : ''}
                <div class="star-picker" id="starPicker">
                    ${[1, 2, 3, 4, 5].map(i => `<i class="far fa-star" data-val="${i}" onclick="setReviewRating(${i})"></i>`).join('')}
                </div>
                <div class="rv-field">
                    <label for="rvText">Your Review *</label>
                    <textarea id="rvText" rows="3" placeholder="Share your experience with this product…"></textarea>
                </div>
                <div class="rv-media-row">
                    <div class="rv-media-item">
                        <label class="rv-media-label"><i class="fas fa-image"></i> Add Photo <span class="rv-media-hint">(optional)</span></label>
                        <input type="file" id="rvImageInput" accept="image/*" onchange="handleReviewImage(event)" class="rv-file-input" />
                        <div id="rvImagePreview" class="rv-media-preview"></div>
                    </div>
                    <div class="rv-media-item">
                        <label class="rv-media-label"><i class="fas fa-video"></i> Add Video <span class="rv-media-hint">(10–15 sec, optional)</span></label>
                        <input type="file" id="rvVideoInput" accept="video/*" onchange="handleReviewVideo(event)" class="rv-file-input" />
                        <div id="rvVideoPreview" class="rv-media-preview"></div>
                        <div id="rvVideoDurationHint" class="rv-duration-hint"></div>
                    </div>
                </div>
                <button class="btn-submit-review" id="submitReviewBtn" onclick="submitReview(${productId})">
                    <i class="fas fa-paper-plane"></i> Submit Review
                </button>
                <p style="font-size:.73rem;color:#94a3b8;margin-top:.5rem;text-align:center;">
                    <i class="fas fa-lock" style="margin-right:.25rem;"></i>Sign in required to submit a review
                </p>
                <div class="rv-alert" id="rvAlert"></div>
            </div>

            <!-- Star filter bar -->
            <div class="rv-filter-bar" id="rvFilterBar">
                <span class="rv-filter-label">Filter:</span>
                <button class="rv-filter-btn active" onclick="applyFilter(0)">All</button>
                ${[5, 4, 3, 2, 1].map(s => `<button class="rv-filter-btn" onclick="applyFilter(${s})">${s} <i class="fas fa-star"></i></button>`).join('')}
            </div>

            <!-- Review list -->
            <div id="reviewList">
                ${count ? reviews.map(reviewCardHtml).join('') : `
                <div class="no-reviews">
                    <i class="far fa-comment-dots"></i>
                    <p>No reviews yet. Be the first to review this product!</p>
                </div>`}
            </div>
        </div>
    </div>`;
}

window.applyFilter = function (star) {
    _activeFilter = star;
    // update button states
    document.querySelectorAll('.rv-filter-btn').forEach(btn => {
        const label = btn.textContent.trim();
        const isAll = label === 'All' && star === 0;
        const isMatch = star > 0 && label.startsWith(String(star));
        btn.classList.toggle('active', isAll || isMatch);
    });
    const filtered = star === 0 ? _allReviews : _allReviews.filter(r => r.rating === star);
    const list = document.getElementById('reviewList');
    if (!list) return;
    if (filtered.length === 0) {
        list.innerHTML = `<div class="no-reviews"><i class="far fa-comment-dots"></i><p>No ${star}★ reviews yet.</p></div>`;
    } else {
        list.innerHTML = filtered.map(reviewCardHtml).join('');
    }
};

function reviewCardHtml(r) {
    const mediaHtml = (r.mediaImageData || r.mediaVideoData) ? `
    <div class="review-media">
        ${r.mediaImageData ? `<img class="review-media-img" src="${r.mediaImageData}" alt="Review photo" onclick="openLightbox('${r.mediaImageData}')" />` : ''}
        ${r.mediaVideoData ? `<video class="review-media-vid" controls preload="metadata"><source src="${r.mediaVideoData}" /></video>` : ''}
    </div>` : '';
    return `
    <div class="review-card">
        <div class="review-header">
            <div class="reviewer-info">
                <span class="reviewer-name">${r.name}</span>
                <div class="review-meta">
                    ${starsHtml(r.rating, 'review-stars')}
                    <span class="review-date">${timeAgo(r.date)}</span>
                </div>
            </div>
            ${r.verified ? `<span class="verified-badge"><i class="fas fa-circle-check"></i> Verified</span>` : ''}
        </div>
        <p class="review-text">${r.text}</p>
        ${mediaHtml}
    </div>`;
}

/* ── Star picker ── */
window.setReviewRating = function (val) {
    _selectedRating = val;
    document.querySelectorAll('#starPicker i').forEach((el, i) => {
        el.className = i < val ? 'fas fa-star selected' : 'far fa-star';
    });
};

/* ── Review media handlers ── */
window.handleReviewImage = function (e) {
    const file = e.target.files[0];
    if (!file) { _reviewImageBase64 = null; return; }
    if (file.size > 8 * 1024 * 1024) {
        alert('Image must be under 8 MB.');
        e.target.value = '';
        _reviewImageBase64 = null;
        return;
    }
    const reader = new FileReader();
    reader.onload = ev => {
        _reviewImageBase64 = ev.target.result;
        const preview = document.getElementById('rvImagePreview');
        if (preview) {
            preview.innerHTML = '';
            const img = document.createElement('img');
            img.alt = 'Preview';
            img.src = _reviewImageBase64;
            preview.appendChild(img);
        }
    };
    reader.readAsDataURL(file);
};

window.handleReviewVideo = function (e) {
    const file = e.target.files[0];
    const hint = document.getElementById('rvVideoDurationHint');
    const preview = document.getElementById('rvVideoPreview');
    if (!file) { _reviewVideoBase64 = null; return; }
    if (file.size > 50 * 1024 * 1024) {
        alert('Video must be under 50 MB.');
        e.target.value = '';
        _reviewVideoBase64 = null;
        return;
    }
    const url = URL.createObjectURL(file);
    const vid = document.createElement('video');
    vid.preload = 'metadata';
    vid.src = url;
    vid.onloadedmetadata = () => {
        URL.revokeObjectURL(url);
        const dur = vid.duration;
        if (dur < 5 || dur > 30) {
            if (hint) { hint.textContent = `⚠ Video is ${dur.toFixed(1)}s. Please use a 10–15 second clip.`; hint.style.color = '#b91c1c'; }
            e.target.value = '';
            _reviewVideoBase64 = null;
            if (preview) preview.innerHTML = '';
            return;
        }
        if (hint) { hint.textContent = `✓ ${dur.toFixed(1)}s — looks great!`; hint.style.color = '#15803d'; }
        const reader = new FileReader();
        reader.onload = ev => {
            _reviewVideoBase64 = ev.target.result;
            if (preview) {
                preview.innerHTML = '';
                const vid = document.createElement('video');
                vid.controls = true;
                vid.preload = 'metadata';
                vid.style.cssText = 'width:100%;border-radius:8px;';
                const src = document.createElement('source');
                src.src = _reviewVideoBase64;
                vid.appendChild(src);
                preview.appendChild(vid);
            }
        };
        reader.readAsDataURL(file);
    };
};

/* ── Submit review ── */
window.submitReview = async function (productId) {
    // Must be logged in to leave a review
    const isLoggedIn = (typeof state !== 'undefined' && state.currentUser)
        || (() => { try { return !!JSON.parse(localStorage.getItem('currentUser') || 'null'); } catch { return false; } })();
    if (!isLoggedIn) {
        // Show the sign-in / sign-up modal from shop.js
        if (typeof openUserModal === 'function') openUserModal();
        return;
    }

    const alertEl = document.getElementById('rvAlert');
    const btn = document.getElementById('submitReviewBtn');
    const text = document.getElementById('rvText').value.trim();

    const showAlert = (msg, type) => {
        alertEl.textContent = msg;
        alertEl.className = `rv-alert ${type}`;
        alertEl.style.display = 'block';
    };

    if (_selectedRating < 1) { showAlert('Please select a star rating.', 'error'); return; }
    if (!text) { showAlert('Please write your review.', 'error'); return; }

    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-circle-notch fa-spin"></i> Submitting…';

    try {
        const res = await fetch(`/api/products/${productId}/reviews`, {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                rating: _selectedRating,
                text,
                verifiedPurchase: false,
                mediaImageData: _reviewImageBase64 || null,
                mediaVideoData: _reviewVideoBase64 || null
            })
        });

        if (!res.ok) throw new Error('Submission failed');

        // Reset form
        document.getElementById('rvText').value = '';
        const imgInput = document.getElementById('rvImageInput');
        const vidInput = document.getElementById('rvVideoInput');
        if (imgInput) imgInput.value = '';
        if (vidInput) vidInput.value = '';
        _reviewImageBase64 = null;
        _reviewVideoBase64 = null;
        const imgPrev = document.getElementById('rvImagePreview');
        const vidPrev = document.getElementById('rvVideoPreview');
        const durHint = document.getElementById('rvVideoDurationHint');
        if (imgPrev) imgPrev.innerHTML = '';
        if (vidPrev) vidPrev.innerHTML = '';
        if (durHint) durHint.textContent = '';
        window.setReviewRating(0);
        showAlert('✅ Thank you! Your review has been submitted.', 'success');
        btn.innerHTML = '<i class="fas fa-check"></i> Submitted!';

        // Reload reviews to show the new one
        setTimeout(() => loadReviews(productId), 800);
    } catch {
        showAlert('Something went wrong. Please try again.', 'error');
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-paper-plane"></i> Submit Review';
    }
};

/* ── Gallery helpers ── */
window.switchImage = function (src, el) {
    const img = document.getElementById('mainImg');
    if (img) img.src = src;
    document.querySelectorAll('.thumb').forEach(t => t.classList.remove('active'));
    el.classList.add('active');
};

window.openLightbox = function (src) {
    const lb = document.getElementById('lightbox');
    const li = document.getElementById('lightboxImg');
    if (lb && li) { li.src = src; lb.style.display = 'flex'; }
};

/* ── Render the cart control for the product detail page ── */
function renderPdCartControl() {
    const ctrl = document.getElementById('pdCartControl');
    if (!ctrl || !_currentProduct) return;
    const pid = String(_currentProduct.id);
    const inStock = _currentProduct.inStock !== false;
    ctrl.innerHTML = '';

    // Out of stock
    if (!inStock) {
        const oos = document.createElement('button');
        oos.className = 'btn-add-cart';
        oos.disabled = true;
        oos.style.cssText = 'background:#e2e8f0;color:#94a3b8;cursor:not-allowed;flex:1';
        oos.textContent = 'Out of Stock';
        ctrl.appendChild(oos);
        return;
    }

    // Read current quantity from cart state (shop.js) or localStorage fallback
    let qty = 0;
    if (typeof state !== 'undefined' && Array.isArray(state.cart)) {
        const item = state.cart.find(i => String(i.productId) === pid);
        qty = item ? (item.quantity || 0) : 0;
    } else {
        try {
            const cart = JSON.parse(localStorage.getItem('cart') || '[]');
            const item = cart.find(i => String(i.productId) === pid);
            qty = item ? (item.quantity || 0) : 0;
        } catch { }
    }

    if (qty <= 0) {
        // Show Add to Cart + Buy Now buttons
        const row = document.createElement('div');
        row.style.cssText = 'display:flex;gap:10px;flex:1;flex-wrap:wrap;';

        const btn = document.createElement('button');
        btn.className = 'btn-add-cart';
        btn.id = 'addCartBtn';
        btn.style.flex = '1';
        btn.innerHTML = '<i class="fas fa-cart-plus"></i> Add to Cart';
        btn.onclick = () => pdAddToCart(pid);

        const buyBtn = document.createElement('button');
        buyBtn.className = 'btn-add-cart';
        buyBtn.style.cssText = 'flex:1;background:linear-gradient(135deg,#f59e0b 0%,#d97706 100%);box-shadow:0 4px 14px rgba(245,158,11,.35);';
        buyBtn.innerHTML = '<i class="fas fa-bolt"></i> Buy Now';
        buyBtn.onclick = () => pdBuyNow(pid);

        row.appendChild(btn);
        row.appendChild(buyBtn);
        ctrl.appendChild(row);
    } else {
        // Show qty picker
        const wrap = document.createElement('div');
        wrap.className = 'qty-picker';

        const dec = document.createElement('button');
        dec.className = 'qty-btn';
        dec.textContent = '−';
        dec.disabled = (qty <= 1);
        dec.onclick = () => pdChangeQty(pid, -1);

        const val = document.createElement('span');
        val.className = 'qty-value';
        val.id = 'qtyValue';
        val.textContent = qty;

        const inc = document.createElement('button');
        inc.className = 'qty-btn';
        inc.textContent = '+';
        inc.onclick = () => pdChangeQty(pid, 1);

        wrap.appendChild(dec);
        wrap.appendChild(val);
        wrap.appendChild(inc);

        // Remove / go-back-to-Add-to-Cart when qty hits 0
        const removeBtn = document.createElement('button');
        removeBtn.className = 'btn btn-sm';
        removeBtn.style.cssText = 'padding:8px 14px;border:1.5px solid #e2e8f0;border-radius:8px;background:#fff;color:#ef4444;cursor:pointer;font-size:13px;font-weight:600';
        removeBtn.innerHTML = '<i class="fas fa-trash"></i>';
        removeBtn.title = 'Remove from cart';
        removeBtn.onclick = () => pdRemoveFromCart(pid);

        ctrl.appendChild(wrap);
        ctrl.appendChild(removeBtn);

        // Buy Now: proceed to checkout with current quantity (no extra add)
        const buyBtn = document.createElement('button');
        buyBtn.className = 'btn-add-cart';
        buyBtn.style.cssText = 'flex:1;background:linear-gradient(135deg,#f59e0b 0%,#d97706 100%);box-shadow:0 4px 14px rgba(245,158,11,.35);';
        buyBtn.innerHTML = '<i class="fas fa-bolt"></i> Buy Now';
        buyBtn.onclick = () => pdBuyNow(pid);
        ctrl.appendChild(buyBtn);
    }
}

function pdChangeQty(pid, delta) {
    let newQty = 0;
    if (typeof updateCartQuantity === 'function') {
        if (typeof state !== 'undefined') {
            const item = state.cart.find(i => String(i.productId) === String(pid));
            newQty = (item ? item.quantity : 0) + delta;
        }
        updateCartQuantity(pid, newQty);
    }
    renderPdCartControl();
}

function pdRemoveFromCart(pid) {
    if (typeof updateCartQuantity === 'function') {
        updateCartQuantity(pid, 0);
    } else {
        try {
            let cart = JSON.parse(localStorage.getItem('cart') || '[]');
            cart = cart.filter(i => String(i.productId) !== String(pid));
            localStorage.setItem('cart', JSON.stringify(cart));
        } catch { }
    }
    renderPdCartControl();
}

/* ── Add to cart ── */
window.pdAddToCart = function (pid) {
    const btn = document.getElementById('addCartBtn');

    if (typeof addToCart === 'function') {
        addToCart(pid, 1);
    } else {
        // Fallback when shop.js is not present
        const p = _currentProduct;
        if (!p) return;
        let cart = [];
        try { cart = JSON.parse(localStorage.getItem('cart') || '[]'); } catch { }
        const existing = cart.find(i => String(i.productId) === String(pid));
        if (existing) existing.quantity += 1;
        else cart.push({ productId: String(pid), quantity: 1, name: p.name, price: p.price, imageUrl: p.imageUrl || '' });
        try { localStorage.setItem('cart', JSON.stringify(cart)); } catch { }
        if (window.refreshCartBadge) window.refreshCartBadge();
    }

    // Switch to qty controls immediately
    renderPdCartControl();

    // Open cart panel so user sees the item was added
    setTimeout(() => {
        const panel = document.getElementById('cartPanel');
        const overlay = document.getElementById('overlay');
        if (panel && !panel.classList.contains('open')) {
            panel.classList.add('open');
            if (overlay) overlay.classList.add('active');
        }
    }, 150);
};

/* ── Buy Now (product detail page) ── */
window.pdBuyNow = function (pid) {
    // Only add to cart if the product is not already there
    const already = (typeof state !== 'undefined' && Array.isArray(state.cart))
        ? state.cart.find(i => String(i.productId) === String(pid))
        : null;
    if (!already) {
        if (typeof addToCart === 'function') {
            addToCart(pid, 1);
        } else {
            const p = _currentProduct;
            if (p) {
                let cart = [];
                try { cart = JSON.parse(localStorage.getItem('cart') || '[]'); } catch { }
                cart.push({ productId: String(pid), quantity: 1, name: p.name, price: p.price, imageUrl: p.imageUrl || '' });
                try { localStorage.setItem('cart', JSON.stringify(cart)); } catch { }
            }
        }
        renderPdCartControl();
    }
    // Navigate to checkout
    if (typeof openCheckout === 'function') {
        openCheckout();
    } else {
        window.location.href = '/Shop/Checkout';
    }
};




document.addEventListener('DOMContentLoaded', loadProduct);
