// Menu Index Page Scripts
(function() {
    const pageConfig = JSON.parse(document.getElementById('menu-page-config')?.textContent || '{}');
    const soldOutLabel = pageConfig.soldOutLabel || 'Tükendi';
    const txtNoActiveOrder = pageConfig.txtNoActiveOrder || "Henüz aktif bir siparişiniz yok.";
    const txtWaiterCalled = pageConfig.txtWaiterCalled || "✅ Garson çağrıldı!";
    const txtGenericError = pageConfig.txtGenericError || "Hata.";
    const txtTryAgain = pageConfig.txtTryAgain || "⚠️ Tekrar deneyin.";
    const txtDiscountedBadge = pageConfig.txtDiscountedBadge || "İNDİRİMLİ";
    const txtRequired = pageConfig.txtRequired || "Zorunlu";
    const txtOptional = pageConfig.txtOptional || "Opsiyonel";
    const txtIncluded = pageConfig.txtIncluded || "Dahil";
    const txtAddedToCart = pageConfig.txtAddedToCart || "✅ Sepete eklendi!";
    const txtConnectionError = pageConfig.txtConnectionError || "❌ Bağlantı hatası.";
    const txtUnknownError = pageConfig.txtUnknownError || "Hata oluştu.";
    const txtHappyHourUpdated = pageConfig.txtHappyHourUpdated || "🎉 Mükemmel! İndirimler güncellendi!";
    const currentTableId = pageConfig.currentTableId || 0;


        // ===== UTILITY =====
        function scrollToTracker() {
            const el = document.getElementById('orderTracker');
            if (el && el.style.display !== 'none') {
                el.scrollIntoView({ behavior: 'smooth', block: 'start' });
            } else {
                showToast(txtNoActiveOrder, true);
            }
        }

        // ===== TOAST (Custom, no Bootstrap) =====
        function showToast(msg, isError) {
            const el = document.getElementById('toastBildirim');
            el.className = 'px-6 py-3 rounded-full shadow-2xl font-headline font-semibold text-sm flex items-center gap-2 transition-all duration-300 text-white ' + (isError ? 'bg-error' : 'bg-primary');
            document.getElementById('toastMesaj').textContent = msg;
            el.style.opacity = '1';
            el.style.transform = 'translateY(0)';
            setTimeout(() => { el.style.opacity = '0'; el.style.transform = 'translateY(16px)'; }, 3000);
        }

        // ===== CATEGORY SCROLL =====
        let scrollObserver = null;

        function toggleCategoryDrawer() {
            const drawer = document.getElementById('categoryDrawer');
            const overlay = document.getElementById('categoryDrawerOverlay');
            if (!drawer || !overlay) return;
            const willOpen = !drawer.classList.contains('active');
            drawer.classList.toggle('active');
            overlay.classList.toggle('active');
            drawer.setAttribute('aria-hidden', willOpen ? 'false' : 'true');
        }

        function closeCategoryDrawer() {
            const drawer = document.getElementById('categoryDrawer');
            const overlay = document.getElementById('categoryDrawerOverlay');
            if (!drawer || !overlay) return;
            drawer.classList.remove('active');
            overlay.classList.remove('active');
            drawer.setAttribute('aria-hidden', 'true');
        }

        function setActiveCategory(catId) {
            document.querySelectorAll('.category-tab').forEach(function(t) {
                t.classList.remove('bg-primary', 'text-on-primary', 'shadow-lg', 'shadow-primary/20');
                t.classList.add('bg-surface-container-low', 'text-on-surface-variant');
            });
            const matchingTab = document.querySelector('.category-tab[data-cat-id="' + catId + '"]');
            if (matchingTab) {
                matchingTab.classList.remove('bg-surface-container-low', 'text-on-surface-variant');
                matchingTab.classList.add('bg-primary', 'text-on-primary', 'shadow-lg', 'shadow-primary/20');
            }

            document.querySelectorAll('.category-drawer-item').forEach(function(item) {
                item.classList.remove('bg-primary', 'text-on-primary');
                item.classList.add('bg-surface-container-low', 'text-on-surface-variant');
            });
            const matchingDrawerItem = document.querySelector('.category-drawer-item[data-cat-id="' + catId + '"]');
            if (matchingDrawerItem) {
                matchingDrawerItem.classList.remove('bg-surface-container-low', 'text-on-surface-variant');
                matchingDrawerItem.classList.add('bg-primary', 'text-on-primary');
            }
        }

        function goToCategory(catId) {
            const section = document.getElementById('cat-' + catId);
            if (!section) return;
            setActiveCategory(catId);
            const y = section.getBoundingClientRect().top + window.pageYOffset - 140;
            window.scrollTo({ top: y, behavior: 'smooth' });
            closeCategoryDrawer();
        }

        function setupScrollObserver() {
            if (scrollObserver) scrollObserver.disconnect();
            const tabs = document.querySelectorAll('.category-tab');
            const sections = document.querySelectorAll('.category-section');

            scrollObserver = new IntersectionObserver(function(entries) {
                entries.forEach(function(entry) {
                    if (entry.isIntersecting) {
                        var catId = entry.target.id.replace('cat-', '');
                        setActiveCategory(catId);
                        var matchingTab = document.querySelector('.category-tab[data-cat-id="' + catId + '"]');
                        if (matchingTab) matchingTab.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
                    }
                });
            }, { rootMargin: '-140px 0px -60% 0px', threshold: 0 });

            sections.forEach(function(s) { scrollObserver.observe(s); });
        }

        document.addEventListener('DOMContentLoaded', function() {
            document.querySelectorAll('.category-tab').forEach(function(btn) {
                btn.addEventListener('click', function() {
                    var catId = this.getAttribute('data-cat-id');
                    goToCategory(catId);
                });
            });

            document.querySelectorAll('.category-drawer-item').forEach(function(btn) {
                btn.addEventListener('click', function() {
                    var catId = this.getAttribute('data-cat-id');
                    goToCategory(catId);
                });
            });

            setupScrollObserver();

            document.addEventListener('keydown', function(e) {
                if (e.key === 'Escape') closeCategoryDrawer();
            });
        });

        // ===== GARSON =====
        async function callWaiter() {
            try {
                const res = await fetch('/siparis/garson-cagir', { method: 'POST' });
                const data = await res.json();
                if (data.success) showToast(txtWaiterCalled);
                else showToast('⚠️ ' + (data.message || txtGenericError), true);
            } catch(e) { showToast(txtTryAgain, true); }
        }

        // ===== SEPETE EKLEME =====
        let currentUrunId = 0, baseFiyat = 0, requiredGroups = [];
        function getCookie(name) {
            const value = `; ${document.cookie}`;
            const parts = value.split(`; ${name}=`);
            if (parts.length === 2) return parts.pop().split(';').shift();
            return null;
        }

        function goBackToQrFlow() {
            const masaNo = getCookie('masa_no');
            if (masaNo && /^\d+$/.test(masaNo)) {
                window.location.href = `/qr/${masaNo}`;
                return;
            }
            window.location.href = '/Menu/Index';
        }

        // Attach click handlers directly to each .btn-add button
        function setupAddButtons() {
            document.querySelectorAll('.btn-add').forEach(function(btn) {
                btn.onclick = function(e) {
                    e.stopPropagation();
                    e.preventDefault();
                    handleAddClick(btn);
                };
            });
        }

        let lastClickedBtn = null;

        function handleAddClick(btn) {
            lastClickedBtn = btn;
            const urunId = parseInt(btn.dataset.urunId);
            const urunAd = btn.dataset.urunAd;
            const fiyat = parseFloat(btn.dataset.fiyat);
            let opsiyonlar = [];
            try { opsiyonlar = JSON.parse(btn.dataset.opsiyonlar || '[]'); } catch(_) {}

            if (!opsiyonlar || opsiyonlar.length === 0) {
                postAddToCart(urunId, 1, []);
                return;
            }

            // Happy Hour check
            const origFiyat = parseFloat(btn.getAttribute('data-orig-fiyat') || fiyat);
            const isHH = fiyat < origFiyat;

            // Show options modal
            currentUrunId = urunId;
            baseFiyat = fiyat;
            document.getElementById('optionsModalTitle').innerHTML = urunAd + (isHH ? ' <span class="text-xs bg-primary text-white px-2 py-0.5 rounded-full ml-2">' + txtDiscountedBadge + '</span>' : '');
            const body = document.getElementById('optionsModalBody');
            body.innerHTML = '';

            const gruplar = {};
            opsiyonlar.forEach(o => { if (!gruplar[o.grup]) gruplar[o.grup] = []; gruplar[o.grup].push(o); });
            requiredGroups = [];

            for (const [grupAdi, opsList] of Object.entries(gruplar)) {
                const isZorunlu = opsList[0].zorunlu === true;
                const isMulti = !isZorunlu;
                const barColor = isZorunlu ? 'bg-secondary' : 'bg-outline-variant';
                const tag = isZorunlu ? '<span class="text-error text-xs font-bold">(' + txtRequired + ')</span>' : '<span class="text-on-surface-variant text-xs">(' + txtOptional + ')</span>';
                const nameAttr = isMulti ? '' : 'grup_' + grupAdi.replace(/\s+/g, '');
                if (!isMulti) requiredGroups.push(nameAttr);

                let html = `<div class="mb-6"><div class="flex items-center gap-3 mb-4"><span class="w-1 h-6 ${barColor} rounded-full"></span><h3 class="font-headline font-bold text-xl uppercase tracking-tighter">${grupAdi} ${tag}</h3></div><div class="grid gap-3">`;

                opsList.forEach(ops => {
                    const id = 'ops_' + ops.id;
                    const inputType = isMulti ? 'checkbox' : 'radio';
                    const inputName = isMulti ? id : nameAttr;
                    const priceStr = ops.ekFiyat > 0
                        ? `<span class="font-headline font-bold text-secondary">+${ops.ekFiyat.toFixed(2)} ₺</span>`
                        : `<span class="font-label text-xs uppercase text-on-surface-variant font-bold">${txtIncluded}</span>`;

                    html += `<label class="flex items-center justify-between p-4 rounded-xl bg-surface-container-low cursor-pointer hover:bg-surface-container-high transition-colors" for="${id}">
                        <div class="flex items-center gap-4">
                            <div class="relative flex items-center">
                                <input class="option-input peer h-6 w-6 rounded border-outline-variant text-primary focus:ring-primary/20 cursor-pointer appearance-none border-2 checked:bg-primary checked:border-primary" type="${inputType}" name="${inputName}" id="${id}" value="${ops.id}" data-price="${ops.ekFiyat}" onchange="updateModalPrice()">
                                <span class="material-symbols-outlined absolute text-white opacity-0 peer-checked:opacity-100 pointer-events-none text-sm left-1" style="font-variation-settings:'wght' 700;">check</span>
                            </div>
                            <span class="font-semibold text-on-surface">${ops.ad}</span>
                        </div>
                        ${priceStr}
                    </label>`;
                });

                html += '</div></div>';
                body.innerHTML += html;
            }
            updateModalPrice();
            document.getElementById('optionsModal').classList.add('active');
        }

        document.addEventListener('DOMContentLoaded', function() {
            setupAddButtons();
            // Card click → navigate to detail page
            document.querySelectorAll('[data-href]').forEach(function(card) {
                card.addEventListener('click', function() {
                    window.location.href = card.dataset.href;
                });
            });
        });

        function closeOptionsModal() {
            document.getElementById('optionsModal').classList.remove('active');
        }

        function updateModalPrice() {
            let toplam = baseFiyat; // baseFiyat zaten indirimli ürün fiyatı
            document.querySelectorAll('.option-input:checked').forEach(input => { 
                toplam += parseFloat(input.dataset.price); // Opsiyonları indirimsiz (tam fiyat) ekle
            });
            document.getElementById('modalTotalPrice').textContent = toplam.toFixed(2) + ' ₺';
            const btn = document.getElementById('btnAddToCartModal');
            const warning = document.getElementById('optionWarning');
            let allSelected = true;
            requiredGroups.forEach(grpName => { if (!document.querySelector('input[name="' + grpName + '"]:checked')) allSelected = false; });
            btn.disabled = !allSelected;
            btn.style.opacity = allSelected ? '1' : '0.5';
            warning.style.display = allSelected ? 'none' : 'block';
        }

        function submitOptionsForm() {
            const selectedOps = [];
            document.querySelectorAll('.option-input:checked').forEach(input => { selectedOps.push(parseInt(input.value)); });
            postAddToCart(currentUrunId, 1, selectedOps);
            closeOptionsModal();
        }

        async function postAddToCart(urunId, adet, opsiyonIds) {
            // Show spinner on the + button
            const btn = lastClickedBtn;
            let origHTML = '';
            if (btn) {
                origHTML = btn.innerHTML;
                btn.innerHTML = '<span class="material-symbols-outlined animate-spin text-lg">progress_activity</span>';
                btn.disabled = true;
                btn.style.pointerEvents = 'none';
            }
            try {
                const response = await fetch('/sepet/ekle', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                    body: JSON.stringify({ UrunId: urunId, Adet: adet, OpsiyonIds: opsiyonIds })
                });
                if (response.status === 401 || response.redirected) {
                    goBackToQrFlow();
                    return;
                }
                const contentType = response.headers.get('content-type') || '';
                if (!contentType.includes('application/json')) {
                    throw new Error('Unexpected response type');
                }
                const data = await response.json();
                if (data.success) {
                    // Checkmark animation
                    if (btn) {
                        btn.innerHTML = '<span class="material-symbols-outlined text-lg" style="font-variation-settings:\'FILL\' 1;">check</span>';
                        btn.classList.remove('bg-primary'); btn.classList.add('bg-green-600');
                        setTimeout(() => { btn.innerHTML = origHTML; btn.disabled = false; btn.style.pointerEvents = ''; btn.classList.remove('bg-green-600'); btn.classList.add('bg-primary'); }, 250);
                    }
                    showToast(txtAddedToCart);
                } else {
                    if (btn) { btn.innerHTML = origHTML; btn.disabled = false; btn.style.pointerEvents = ''; }
                    showToast('❌ ' + (data.message || txtUnknownError), true);
                }
            } catch (err) {
                console.error(err);
                if (btn) { btn.innerHTML = origHTML; btn.disabled = false; btn.style.pointerEvents = ''; }
                showToast(txtConnectionError, true);
            }
            lastClickedBtn = null;
        }

        // ===== SIGNALR =====
        const menuConnection = new signalR.HubConnectionBuilder().withUrl('/hubs/order').withAutomaticReconnect([0,2000,5000,10000,30000]).configureLogging(signalR.LogLevel.Error).build();

        menuConnection.on('HappyHourGuncellendi', async function() {
            try { const res = await fetch('/menu/json'); triggerYenidenCiz(await res.json()); showToast(txtHappyHourUpdated); } catch(e) { console.error(e); }
        });

        menuConnection.on('MenuGuncellendi', async function() {
            try { const res = await fetch('/menu/json'); triggerYenidenCiz(await res.json()); } catch(e) { console.error(e); }
        });

        function triggerYenidenCiz(data) {
            yenidenCiz(data.kategoriler, data.happyHour);
        }

        function yenidenCiz(kategoriler, happyHour) {
            const tabScroll = document.querySelector('nav.sticky');
            const drawerList = document.getElementById('categoryDrawerList');
            if (!tabScroll) return;
            tabScroll.innerHTML = '';
            if (drawerList) drawerList.innerHTML = '';
            kategoriler.forEach(function(kat, i) {
                const btn = document.createElement('button');
                btn.className = 'category-tab px-6 py-2.5 rounded-full font-headline font-bold text-sm whitespace-nowrap transition-all active:scale-95 ' + (i === 0 ? 'bg-primary text-on-primary shadow-lg shadow-primary/20' : 'bg-surface-container-low text-on-surface-variant hover:bg-surface-container-high');
                btn.setAttribute('data-cat-id', kat.id);
                btn.textContent = kat.ad;
                btn.addEventListener('click', function() {
                    goToCategory(kat.id);
                });
                tabScroll.appendChild(btn);

                if (drawerList) {
                    const dBtn = document.createElement('button');
                    dBtn.className = 'category-drawer-item w-full text-left px-4 py-3 rounded-2xl font-headline font-bold text-sm bg-surface-container-low text-on-surface-variant hover:bg-surface-container-high transition-colors';
                    dBtn.setAttribute('data-cat-id', kat.id);
                    dBtn.textContent = kat.ad;
                    dBtn.addEventListener('click', function() {
                        goToCategory(kat.id);
                    });
                    drawerList.appendChild(dBtn);
                }
            });

            document.querySelectorAll('.category-section').forEach(el => el.remove());
            const main = document.querySelector('main');
            kategoriler.forEach(function(kat) {
                if (!kat.urunler || kat.urunler.length === 0) return;
                const section = document.createElement('div');
                section.className = 'category-section mb-12';
                section.id = 'cat-' + kat.id;
                let html = '<h2 class="font-headline text-2xl font-extrabold text-on-surface mb-6">' + escapeHtml(kat.ad) + '</h2><div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">';
                kat.urunler.forEach(function(urun) {
                    const isSoldOut = !urun.aktifMi;
                    let bFiyat = urun.fiyat;
                    const hhUrunIds = (happyHour && Array.isArray(happyHour.urunIds)) ? happyHour.urunIds : [];
                    let hhGecerli = happyHour != null && (hhUrunIds.length === 0 || hhUrunIds.includes(urun.id));
                    if (hhGecerli) {
                        bFiyat = Math.round(bFiyat * (1 - happyHour.indirimOrani / 100) * 100) / 100;
                    }
                    const fiyatStr = urun.fiyat.toFixed(2);
                    const gorunenFiyatStr = bFiyat.toFixed(2);
                    const cardClick = isSoldOut ? '' : ` onclick="window.location.href='/Menu/Detay/${urun.id}'"`;
                    html += `<div class="group relative bg-surface-container-lowest rounded-3xl overflow-hidden transition-all duration-300 hover:shadow-2xl ${isSoldOut ? 'opacity-50 pointer-events-none' : ''} cursor-pointer"${cardClick}>`;
                    html += '<div class="aspect-[4/3] w-full overflow-hidden">';
                    if (urun.gorselUrl) html += `<img alt="${escapeHtml(urun.ad)}" class="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105" src="${escapeHtml(urun.gorselUrl)}" />`;
                    else html += '<div class="w-full h-full bg-gradient-to-br from-surface-container-low to-surface-container flex items-center justify-center text-6xl">☕</div>';
                    html += '</div><div class="p-6">';
                    html += `<div class="flex justify-between items-start mb-2"><h3 class="font-headline font-bold text-xl text-on-surface leading-tight">${escapeHtml(urun.ad)}</h3>`;
                    
                    html += `<div class="flex flex-col items-end"><span class="font-headline font-bold text-primary text-lg whitespace-nowrap ml-3">${gorunenFiyatStr} ₺</span>`;
                    if(hhGecerli) { html += `<span class="text-xs text-error font-bold line-through">${fiyatStr} ₺</span>`; }
                    html += `</div></div>`;
                    
                    html += `<p class="text-on-surface-variant text-sm line-clamp-2 mb-6">${escapeHtml(urun.aciklama || '')}</p>`;
                    html += '<div class="flex items-center justify-between"><div class="flex items-center gap-1">';
                    if (urun.populerMi) html += '<span class="material-symbols-outlined text-secondary text-sm" style="font-variation-settings:\'FILL\' 1;">local_fire_department</span><span class="text-xs font-bold text-on-surface">Popüler</span>';
                    if (urun.kalori) html += `<span class="text-xs font-semibold text-on-surface-variant bg-surface-container-low px-2 py-0.5 rounded-full">🔥 ${urun.kalori} kcal</span>`;
                    if (isSoldOut) html += `<span class="text-xs font-bold text-error">${soldOutLabel}</span>`;
                    html += "</div>";
                    if (!isSoldOut) {
                        const opsAttr = JSON.stringify(urun.opsiyonlar || []).replace(/'/g, '&#39;');
                        html += `<button class="btn-add flex items-center justify-center w-10 h-10 rounded-2xl bg-primary text-on-primary hover:bg-primary-dim active:scale-90 transition-all shadow-md shadow-primary/20" 
                                    data-urun-id="${urun.id}" 
                                    data-urun-ad="${escapeHtml(urun.ad)}" 
                                    data-fiyat="${gorunenFiyatStr}" 
                                    data-orig-fiyat="${fiyatStr}"
                                    data-opsiyonlar='${opsAttr}'>
                                    <span class="material-symbols-outlined">add</span>
                                 </button>`;
                    }
                    html += '</div></div></div>';
                });
                html += '</div>';
                section.innerHTML = html;
                main.appendChild(section);
            });
            setupScrollObserver();
            setupAddButtons();
        }

        function escapeHtml(str) { if (!str) return ''; const d = document.createElement('div'); d.textContent = str; return d.innerHTML; }
        
        function toggleLang() {
            var isEn = document.cookie.indexOf('lang=en') !== -1;
            document.cookie = "lang=" + (isEn ? "tr" : "en") + ";path=/;max-age=31536000";
            window.location.reload();
        }
        
        menuConnection.start()
            .then(async () => {
                if (currentTableId > 0) {
                    try {
                        await menuConnection.invoke('JoinTableGroup', currentTableId);
                    } catch (err) {
                        console.error('Table group join failed:', err);
                    }
                }
            })
            .catch(err => console.error('SignalR:', err));



})();
