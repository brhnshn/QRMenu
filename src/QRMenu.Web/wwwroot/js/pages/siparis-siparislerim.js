// Siparislerim Page Scripts
(function() {
    const siparisConfig = JSON.parse(document.getElementById('siparis-page-config')?.textContent || '{}');
    const i18n = siparisConfig.i18n || {};
    function L(key, fallback) { return i18n[key] || fallback || key; }

        const stepDefs = [
            { durum: 'Onaylandi', label: L('step1Label', 'Alındı'), sublabel: L('step1Sublabel', 'Siparişiniz mutfağa ulaştı'), icon: 'task_alt' },
            { durum: 'Hazirlaniyor', label: L('step2Label', 'Hazırlanıyor'), sublabel: L('step2Sublabel', 'Mutfak ekibi yemeğinizi hazırlıyor'), icon: 'restaurant' },
            { durum: 'Hazir', label: L('step3Label', 'Hazır'), sublabel: L('step3Sublabel', 'Servis için hazırlanıyor'), icon: 'doorbell' },
            { durum: 'TeslimEdildi', label: L('step4Label', 'Teslim Edildi'), sublabel: L('step4Sublabel', 'Afiyet olsun!'), icon: 'celebration' }
        ];
        const stepOrder = { Onaylandi: 1, Hazirlaniyor: 2, Hazir: 3, TeslimEdildi: 4 };
        const currentTableId = siparisConfig.currentTableId || 0;

        const heroMessages = {
            1: { title: L('hero1Title', 'Siparişiniz alındı.'), subtitle: L('hero1Subtitle', 'Mutfak ekibimiz siparişinizi aldı.') },
            2: { title: L('hero2Title', 'Yemekleriniz hazırlanıyor.'), subtitle: L('hero2Subtitle', 'Şeflerimiz lezzetli yemeklerinizi hazırlıyor.') },
            3: { title: L('hero3Title', 'Siparişiniz hazır!'), subtitle: L('hero3Subtitle', 'Servis ekibi masanıza doğru yola çıktı.') },
            4: { title: L('hero4Title', 'Afiyet olsun!'), subtitle: L('hero4Subtitle', 'Siparişiniz teslim edildi. Tekrar bekleriz!') }
        };

        function renderOrders(siparisler) {
            const container = document.getElementById('ordersContainer');
            const heroTitle = document.getElementById('heroTitle');
            const heroSubtitle = document.getElementById('heroSubtitle');
            const heroRing = document.getElementById('heroRing');
            const helpSection = document.getElementById('helpSection');
            const emptyState = document.getElementById('emptyState');
            const heroSection = document.getElementById('heroSection');

            // Filter active orders
            const aktif = siparisler.filter(s => stepOrder[s.durum] !== undefined);

            if (aktif.length === 0) {
                heroSection.style.display = 'none';
                helpSection.style.display = 'none';
                container.innerHTML = '';
                emptyState.style.display = 'block';
                return;
            }

            emptyState.style.display = 'none';
            heroSection.style.display = 'block';
            helpSection.style.display = 'flex';

            // Hero reflects the latest order
            const latest = aktif[0];
            const latestStep = stepOrder[latest.durum] || 1;
            const msg = heroMessages[latestStep] || heroMessages[1];
            heroTitle.textContent = msg.title;
            heroSubtitle.textContent = msg.subtitle;

            // Stop ping if delivered
            if (latestStep === 4) {
                heroRing.classList.remove('animate-ping');
                heroRing.classList.add('opacity-0');
            } else {
                heroRing.classList.add('animate-ping');
                heroRing.classList.remove('opacity-0');
            }

            // Update help text
            const masaBadge = document.querySelector('.font-label');
            const masaNo = masaBadge ? masaBadge.textContent.replace(L('masa', 'Masa') + ' ', '') : '';
            document.getElementById('helpText').textContent = L('helpText', "Butona basarak garsonunuzu Masa {0}'a çağırabilirsiniz.").replace('{0}', masaNo);

            // Render each order (Stitch Progress Tracker)
            container.innerHTML = aktif.map(s => {
                const current = stepOrder[s.durum] || 0;
                const progressPercent = Math.max(0, ((current - 1) / (stepDefs.length - 1)) * 100);

                // Time Remaining Card (Stitch)
                let timeCard = '';
                if (current < 4) {
                    timeCard = `
                        <section class="bg-surface-container-lowest rounded-3xl p-8 shadow-[0_4px_24px_rgba(0,0,0,0.04)] mb-10 text-center relative overflow-hidden">
                            <div class="absolute top-0 left-0 w-1.5 h-full bg-primary"></div>
                            <span class="text-xs font-label font-bold uppercase tracking-widest text-on-surface-variant block mb-2">${L('orderNumber', 'Sipariş')} #${s.siparisId}</span>
                            <div class="flex items-center justify-center gap-3">
                                <span class="font-headline text-3xl font-extrabold text-primary">${s.toplamTutar.toFixed(2)}</span>
                                <span class="font-headline text-lg font-bold text-on-surface mt-1">₺</span>
                            </div>
                            <p class="text-xs text-on-surface-variant mt-2">${s.olusturmaTarihi}</p>
                        </section>`;
                }

                // Detail items
                let detailsHtml = '';
                if (s.detaylar && s.detaylar.length > 0) {
                    detailsHtml = `<div class="mb-6 bg-surface-container-lowest rounded-2xl p-4">${s.detaylar.map(d =>
                        `<div class="flex justify-between items-center py-2 ${s.detaylar.indexOf(d) > 0 ? 'border-t border-surface-container' : ''}">
                            <span class="font-medium text-on-surface">${d.adet}x ${escapeHtml(d.urunAd)}</span>
                            <span class="font-label font-bold text-on-surface-variant">${d.birimFiyat.toFixed(2)} ₺</span>
                        </div>`
                    ).join('')}</div>`;
                }
                
                let gameHtml = '';
                const allowedStatus = ['Onaylandi', 'Hazirlaniyor', 'Hazir', 'TeslimEdildi'];
                if (allowedStatus.includes(s.durum) && s.oyunOynandiMi === false) {
                    let titleMsg = L('prepTitle', 'Siparişin Hazırlanıyor!');
                    let descMsg = L('prepDesc', 'Beklerken şansını dene, anında indirim kazan!');
                    
                    if (s.durum === 'TeslimEdildi' || s.durum === 'Hazir') {
                        titleMsg = L('readyTitle', 'Siparişin Hazır!');
                        descMsg = L('readyDesc', 'Şansını dene, hesabına indirim uygula!');
                    }

                    gameHtml = `
                    <div class="mb-6 bg-secondary-container rounded-3xl p-6 text-center shadow-lg border border-secondary/20 relative overflow-hidden">
                        <div class="absolute inset-0 bg-white/20 blur-md pointer-events-none"></div>
                        <h4 class="font-headline font-extrabold text-xl text-on-secondary-container mb-2 relative">🎉 ${titleMsg}</h4>
                        <p class="text-sm text-on-secondary-container mb-4 relative">${descMsg}</p>
                        <button onclick="playGame(${s.siparisId})" class="relative inline-flex items-center gap-2 bg-secondary text-primary-container px-6 py-3 rounded-full font-headline font-bold shadow-lg active:scale-95 transition-transform">
                            <span class="material-symbols-outlined text-xl">casino</span>
                            ${L('tryYourLuck', 'Şansını Dene')}
                        </button>
                    </div>`;
                }

                // Progress Tracker (Stitch — exact copy)
                const stepsHtml = stepDefs.map((st, i) => {
                    const stepNum = i + 1;
                    const isDone = stepNum < current;
                    const isCurrent = stepNum === current;
                    const dotBg = isDone || isCurrent
                        ? 'bg-primary text-on-primary shadow-lg shadow-primary/20'
                        : 'bg-surface-container-highest text-on-surface-variant';
                    const opacityClass = (!isDone && !isCurrent) ? 'opacity-50' : '';
                    const activeClass = isCurrent ? 'step-active' : '';
                    const fillStyle = (isDone || isCurrent) ? "font-variation-settings: 'FILL' 1;" : '';

                    return `<div class="flex items-start gap-6 group ${activeClass} ${opacityClass}">
                        <div class="relative z-10 w-12 h-12 rounded-full ${dotBg} flex items-center justify-center">
                            <span class="material-symbols-outlined" style="${fillStyle}">${st.icon}</span>
                        </div>
                        <div>
                            <h4 class="font-headline font-bold text-on-surface">${st.label}</h4>
                            <p class="text-sm text-on-surface-variant">${st.sublabel}</p>
                        </div>
                    </div>`;
                }).join('');

                return `
                    ${timeCard}
                    ${detailsHtml}
                    ${gameHtml}
                    <section class="mb-12">
                        <div class="flex flex-col gap-8 relative">
                            <!-- Vertical Line (Stitch) -->
                            <div class="absolute left-[23px] top-4 bottom-4 w-0.5 bg-surface-container-highest"></div>
                            <div class="absolute left-[23px] top-4 w-0.5 bg-primary" style="height:${progressPercent}%"></div>
                            ${stepsHtml}
                        </div>
                    </section>`;
            }).join('<hr class="border-surface-container my-6" />');
        }

        function escapeHtml(str) { if (!str) return ''; const d = document.createElement('div'); d.textContent = str; return d.innerHTML; }

        // Fetch orders
        async function fetchOrders() {
            try {
                const res = await fetch('/siparis/siparislerim', {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (res.status === 401 || res.redirected) {
                    document.getElementById('heroTitle').textContent = L('sessionNotFound', 'Oturum bulunamadı');
                    document.getElementById('heroSubtitle').textContent = L('scanAgain', 'Lütfen QR kodu tekrar okutun.');
                    return;
                }
                if (!res.ok) throw new Error('Network error');
                
                const data = await res.json();
                if (data.success) renderOrders(data.siparisler);
            } catch(e) {
                console.error(e);
                document.getElementById('heroTitle').textContent = L('connectionError', 'Bağlantı hatası');
                document.getElementById('heroSubtitle').textContent = L('shouldRefresh', 'Sayfa yenilensin mi?');
            }
        }

        document.addEventListener('DOMContentLoaded', fetchOrders);

        // SignalR
        const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/order').withAutomaticReconnect([0,2000,5000,10000,30000]).configureLogging(signalR.LogLevel.Error).build();
        connection.on('SiparisGuncellendi', fetchOrders);
        connection.start()
            .then(async () => {
                if (currentTableId > 0) {
                    try {
                        await connection.invoke('JoinTableGroup', currentTableId);
                    } catch (err) {
                        console.error('Table group join failed:', err);
                    }
                }
            })
            .catch(err => console.error('SignalR:', err));

        // Garson Çağır
        async function callWaiter() {
            try {
                const res = await fetch('/siparis/garson-cagir', { 
                    method: 'POST',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                
                if (res.status === 401 || res.redirected) {
                    showToast('⚠️ ' + L('scanQr', 'Lütfen QR kodu okutun'), true);
                    return;
                }
                
                const data = await res.json();
                showToast(data.success ? '✅ ' + L('waiterCalled', 'Garson çağrıldı!') : '⚠️ ' + (data.message || L('error', 'Hata.')), !data.success);
            } catch(e) { showToast('⚠️ ' + L('tryAgain', 'Tekrar deneyin.'), true); }
        }

        let currentGameOrderId = 0;
        let currentGames = [];
        let memoryTimer = null;

        function closeGameModal() {
            const modal = document.getElementById('gameModal');
            modal.style.display = 'none';
            if (memoryTimer) {
                clearInterval(memoryTimer);
                memoryTimer = null;
            }
        }

        async function playGame(siparisId) {
            currentGameOrderId = siparisId;
            const modal = document.getElementById('gameModal');
            const stage = document.getElementById('gameStage');
            document.getElementById('gameTitle').textContent = '🎮 ' + L('chanceGames', 'Şans Oyunları');
            modal.style.display = 'flex';
            stage.innerHTML = '<div class="text-center py-8">' + L('loadingGames', 'Oyunlar yükleniyor...') + '</div>';

            try {
                const res = await fetch(`/siparis/oyun-secimleri/${siparisId}`, {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const data = await res.json();

                if (!data.success) {
                    stage.innerHTML = `<div class="text-center py-8">⚠️ ${data.message || L('genericError', 'Bir hata oluştu.')}</div>`;
                    return;
                }

                currentGames = data.oyunlar || [];
                renderGameSelection();
            } catch (e) {
                console.error(e);
                stage.innerHTML = '<div class="text-center py-8">⚠️ ' + L('connectionErrorDot', 'Bağlantı hatası.') + '</div>';
            }
        }

        function renderGameSelection() {
            const stage = document.getElementById('gameStage');
            const cards = currentGames.map(g => {
                const btn = g.aktif
                    ? `<button class="mt-3 w-full bg-primary text-white rounded-xl py-2 font-bold" onclick="startSelectedGame('${g.tip}')">${L('selectGame', 'Oyunu Seç')}</button>`
                    : `<button class="mt-3 w-full bg-gray-300 text-gray-600 rounded-xl py-2 font-bold" disabled>${L('passive', 'Pasif')}</button>`;

                return `
                <div class="game-card bg-surface-container-lowest rounded-2xl p-4 border border-surface-container mb-3">
                    <div class="flex items-center justify-between">
                        <h4 class="font-headline text-lg font-extrabold">${g.ad}</h4>
                        <span class="text-xs px-2 py-1 rounded-full ${g.aktif ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}">${g.aktif ? L('active', 'Aktif') : L('passive', 'Pasif')}</span>
                    </div>
                    <p class="text-sm text-on-surface-variant mt-1">${g.aciklama || ''}</p>
                    ${btn}
                </div>`;
            }).join('');

            stage.innerHTML = `
                <p class="text-sm text-on-surface-variant mb-3">${L('gameInstruction', 'Bir oyun seç ve şansını dene. Bir siparişte sadece bir kez oynayabilirsin.')}</p>
                ${cards}
            `;
        }

        function startSelectedGame(tip) {
            const game = currentGames.find(g => g.tip === tip);
            if (!game || !game.aktif) return;

            if (tip === 'CARKIFELEK') startWheelGame(game);
            else if (tip === 'HAFIZA') startMemoryGame(game);
            else startScratchGame(game);
        }

        async function completeGame(oyunTipi, basariliMi, oyunToken, hafizaHamleleri) {
            const res = await fetch(`/siparis/oyun-sonuclandir/${currentGameOrderId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                body: JSON.stringify({ oyunTipi, basariliMi, oyunToken, hafizaHamleleri: hafizaHamleleri || [] })
            });
            return await res.json();
        }

        function showResultWithCountdown(resultEl, html, seconds, onDone) {
            let remaining = seconds;

            function render() {
                resultEl.innerHTML = `${html}<div class="mt-2 text-sm opacity-80">${L('closing', 'Kapanıyor')}: ${remaining} sn</div>`;
            }

            render();
            const intervalId = setInterval(() => {
                remaining--;
                if (remaining <= 0) {
                    clearInterval(intervalId);
                    onDone();
                    return;
                }
                render();
            }, 1000);
        }

        function startWheelGame(game) {
            const stage = document.getElementById('gameStage');
            const oduller = game.oduller || [];
            if (!oduller.length) {
                stage.innerHTML = '<div class="text-center py-8">⚠️ ' + L('wheelNoReward', 'Çarkıfelek için ödül bulunmuyor.') + '</div>';
                return;
            }

            stage.innerHTML = `
                <div class="text-center">
                    <p class="mb-3 text-sm text-on-surface-variant">${L('wheelInstruction', 'Çarkıfeleği döndür butonuna bas, çark durunca ödülün belli olacak.')}</p>
                    <div class="relative mx-auto" style="width:min(88vw,360px);height:min(88vw,360px);">
                        <canvas id="wheelCanvas" width="360" height="360" style="width:100%;height:100%;"></canvas>
                        <div style="position:absolute;left:50%;top:-6px;transform:translateX(-50%);font-size:34px;">▼</div>
                    </div>
                    <button id="spinBtn" class="mt-5 bg-primary text-white rounded-xl px-7 py-3.5 text-base font-bold">${L('spinWheel', 'Çarkıfeleği Döndür')}</button>
                    <div id="wheelResult" class="mt-4 text-base font-semibold"></div>
                </div>`;

            const canvas = document.getElementById('wheelCanvas');
            const ctx = canvas.getContext('2d');
            let angle = 0;
            const colors = ['#fd7954', '#ffca57', '#ec6d49', '#ffd88f', '#ff9f6e', '#f7b84b'];
            const weights = oduller.map(o => Math.max(0.01, parseFloat(o.ihtimal) || 0.01));
            const totalWeight = weights.reduce((sum, w) => sum + w, 0);
            const slices = [];
            let cumulative = 0;
            for (let i = 0; i < oduller.length; i++) {
                const span = (weights[i] / totalWeight) * Math.PI * 2;
                slices.push({ start: cumulative, end: cumulative + span, mid: cumulative + (span / 2) });
                cumulative += span;
            }

            function drawWheel(a) {
                ctx.clearRect(0, 0, 360, 360);
                for (let i = 0; i < oduller.length; i++) {
                    const start = a + slices[i].start;
                    const end = a + slices[i].end;
                    const span = slices[i].end - slices[i].start;
                    ctx.beginPath();
                    ctx.moveTo(180, 180);
                    ctx.arc(180, 180, 166, start, end);
                    ctx.closePath();
                    ctx.fillStyle = colors[i % colors.length];
                    ctx.fill();

                    ctx.save();
                    ctx.translate(180, 180);
                    ctx.rotate(a + slices[i].mid);
                    ctx.fillStyle = '#2e2f2f';
                    ctx.textAlign = 'right';
                    ctx.textBaseline = 'middle';
                    const fontSize = span < 0.24 ? 9 : span < 0.45 ? 11 : 13;
                    ctx.font = `bold ${fontSize}px sans-serif`;
                    const text = (oduller[i].odulTanim || '').trim();
                    ctx.fillText(text, 148, 0);
                    ctx.restore();
                }
                ctx.beginPath();
                ctx.arc(180, 180, 30, 0, Math.PI * 2);
                ctx.fillStyle = '#fff';
                ctx.fill();
            }

            function animateToIndex(targetIndex, done) {
                const targetSlice = slices[targetIndex] || slices[0];
                const desired = (Math.PI * 1.5) - targetSlice.mid;
                const start = angle;
                const end = desired + (Math.PI * 2 * 6);
                const duration = 3500;
                const t0 = performance.now();

                function frame(t) {
                    const p = Math.min(1, (t - t0) / duration);
                    const eased = 1 - Math.pow(1 - p, 4);
                    angle = start + (end - start) * eased;
                    drawWheel(angle);
                    if (p < 1) requestAnimationFrame(frame);
                    else done();
                }
                requestAnimationFrame(frame);
            }

            drawWheel(angle);

            document.getElementById('spinBtn').onclick = async () => {
                const btn = document.getElementById('spinBtn');
                const result = document.getElementById('wheelResult');
                btn.disabled = true;
                btn.classList.add('opacity-60');
                result.textContent = L('wheelSpinning', 'Çark dönüyor...');

                try {
                    const data = await completeGame('CARKIFELEK', true, game.oyunToken, []);
                    if (!data.success) {
                        result.textContent = '⚠️ ' + (data.message || L('error', 'Hata.'));
                        return;
                    }
                    const hedefIndex = Number.isInteger(data.hedefIndex) ? data.hedefIndex : Math.floor(Math.random() * oduller.length);
                    animateToIndex(hedefIndex, () => {
                        const gelenDilim = data.odulTanim ? `<div class="mb-1">🎯 ${L('landedSlice', 'Gelen dilim')}: <strong>${data.odulTanim}</strong></div>` : '';
                        const html = data.kazandiMi
                            ? `${gelenDilim}<div>🎉 ${data.message}</div><small>${L('newTotal', 'Yeni tutar')}: ${data.yeniTutar.toFixed(2)} ₺</small>`
                            : `${gelenDilim}<div>😢 ${data.message}</div>`;
                        showResultWithCountdown(result, html, 6, () => { closeGameModal(); fetchOrders(); });
                    });
                } catch (e) {
                    console.error(e);
                    result.textContent = '⚠️ ' + L('connectionErrorDot', 'Bağlantı hatası.');
                }
            };
        }

        function startMemoryGame(game) {
            const stage = document.getElementById('gameStage');
            const cards = (game.hafizaKartlari || []);
            if (cards.length < 16) {
                stage.innerHTML = '<div class="text-center py-8">⚠️ ' + L('memoryGameMinImages', 'Hafıza oyunu için en az 8 ürün görseli gerekli.') + '</div>';
                return;
            }

            stage.innerHTML = `
                <div class="mb-2 text-sm text-on-surface-variant">${L('memoryInstruction', '16 kartı 45 saniyede eşleştir.')}</div>
                <div class="mb-3 font-bold">⏳ <span id="memoryTime">45</span> sn</div>
                <div id="memoryGrid" class="memory-grid"></div>
                <div id="memoryResult" class="mt-3 font-semibold"></div>
            `;

            const grid = document.getElementById('memoryGrid');
            const result = document.getElementById('memoryResult');
            let first = null;
            let lock = false;
            let matched = 0;
            let seconds = 45;
            const memoryMoves = [];

            grid.innerHTML = cards.map((c, idx) => `
                <div class="memory-card" data-index="${idx}" data-pair="${c.eslesmeId}">
                    <div class="memory-face memory-back">QR</div>
                    <div class="memory-face memory-front">
                        <img src="${c.gorselUrl}" alt="${escapeHtml(c.urunAd)}" style="width:100%;height:100%;object-fit:cover;" />
                    </div>
                </div>
            `).join('');

            if (memoryTimer) clearInterval(memoryTimer);
            memoryTimer = setInterval(async () => {
                seconds--;
                const timeEl = document.getElementById('memoryTime');
                if (timeEl) timeEl.textContent = seconds.toString();
                if (seconds <= 0) {
                    clearInterval(memoryTimer);
                    memoryTimer = null;
                    result.textContent = '😢 ' + L('timeExpired', 'Süre doldu, kazanamadınız.');
                    const data = await completeGame('HAFIZA', false, game.oyunToken, memoryMoves);
                    if (data?.message) result.textContent = `😢 ${data.message}`;
                    showResultWithCountdown(result, result.innerHTML || result.textContent, 6, () => { closeGameModal(); fetchOrders(); });
                }
            }, 1000);

            grid.querySelectorAll('.memory-card').forEach(card => {
                card.addEventListener('click', async () => {
                    if (seconds <= 0 || lock || card.classList.contains('flipped') || card.dataset.done === '1') return;
                    card.classList.add('flipped');

                    if (!first) {
                        first = card;
                        return;
                    }

                    if (first.dataset.pair === card.dataset.pair) {
                        memoryMoves.push({
                            ilkIndex: parseInt(first.dataset.index),
                            ikinciIndex: parseInt(card.dataset.index)
                        });
                        first.dataset.done = '1';
                        card.dataset.done = '1';
                        matched++;
                        first = null;

                        if (matched === 8) {
                            if (memoryTimer) {
                                clearInterval(memoryTimer);
                                memoryTimer = null;
                            }
                            const data = await completeGame('HAFIZA', true, game.oyunToken, memoryMoves);
                            const html = data.kazandiMi
                                ? `🎉 ${data.message}<br><small>${L('newTotal', 'Yeni tutar')}: ${data.yeniTutar.toFixed(2)} ₺</small>`
                                : `😢 ${data.message}`;
                            showResultWithCountdown(result, html, 6, () => { closeGameModal(); fetchOrders(); });
                        }
                    } else {
                        lock = true;
                        const second = card;
                        const firstCard = first;
                        first = null;
                        setTimeout(() => {
                            firstCard.classList.remove('flipped');
                            second.classList.remove('flipped');
                            lock = false;
                        }, 700);
                    }
                });
            });
        }

        function startScratchGame(game) {
            const stage = document.getElementById('gameStage');
            
            // İlk Adım: 9 adet kartı göster.
            stage.innerHTML = `
                <p class="text-sm text-on-surface-variant mb-3 text-center" id="scratchGridInst">${L('scratchInstruction1', 'Aşağıdaki kartlardan birini seçin!')}</p>
                <div id="scratchGridContainer" class="grid grid-cols-3 gap-2 mb-4">
                    ${Array.from({ length: 9 }).map((_, i) => `
                        <button class="bg-surface-container-highest rounded-xl h-24 flex items-center justify-center font-bold text-on-surface hover:bg-primary hover:text-white transition-colors duration-300 shadow-sm border border-outline-variant scratch-select-btn" data-index="${i}">
                            <span class="text-2xl block mb-1">🎁</span>
                        </button>
                    `).join('')}
                </div>
                <div id="scratchPlayArea" class="hidden">
                    <p class="text-sm text-on-surface-variant mb-3 text-center">${L('scratchInstruction2', 'Seçtiğin kartı parmağınızla kazıyın!')}</p>
                    <div class="relative w-full max-w-[320px] h-[160px] mx-auto rounded-xl overflow-hidden shadow-md bg-surface-container-highest flex items-center justify-center select-none transform transition-all duration-500 scale-95" id="scratchCanvasContainer" style="touch-action: none;">
                        <div id="scratchPrize" class="absolute inset-0 flex flex-col items-center justify-center p-4 text-center opacity-0 transition-opacity duration-300">
                            <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary" id="scratchLoading"></div>
                            <span id="scratchText" class="font-headline font-extrabold text-2xl hidden"></span>
                        </div>
                        <canvas id="scratchCanvas" class="absolute inset-0 w-full h-full cursor-pointer z-10" width="320" height="160"></canvas>
                    </div>
                </div>
                <div id="scratchResultMsg" class="mt-4 text-center font-bold text-sm min-h-[3rem]"></div>
            `;

            const gridContainer = document.getElementById('scratchGridContainer');
            const playArea = document.getElementById('scratchPlayArea');
            const canvasContainer = document.getElementById('scratchCanvasContainer');
            const instText = document.getElementById('scratchGridInst');
            const resultMsg = document.getElementById('scratchResultMsg');
            let isGameStarted = false;

            document.querySelectorAll('.scratch-select-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    if (isGameStarted) return;
                    isGameStarted = true;
                    
                    // Görsel geçiş
                    gridContainer.classList.add('hidden');
                    instText.classList.add('hidden');
                    playArea.classList.remove('hidden');
                    
                    // Hafif büyük animasyonu
                    setTimeout(() => {
                        canvasContainer.classList.remove('scale-95');
                        canvasContainer.classList.add('scale-100');
                    }, 50);

                    initScratchCanvas();
                });
            });

            function initScratchCanvas() {
                const canvas = document.getElementById('scratchCanvas');
                const ctx = canvas.getContext('2d');
                const prizeDiv = document.getElementById('scratchPrize');
                const textDiv = document.getElementById('scratchText');
                const loadDiv = document.getElementById('scratchLoading');
                
                // Fill with silver layer
                ctx.fillStyle = '#94a3b8';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                
                // Add some pattern/text
                ctx.font = 'bold 24px sans-serif';
                ctx.fillStyle = '#475569';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(L('scratchCardText', 'KAZI KAZAN'), canvas.width/2, canvas.height/2);

                let isDrawing = false;
                let resultFetched = false;
                let fetchPromise = null;
                let resultData = null;
                let finished = false;

                const fetchResultOnce = () => {
                    if (!fetchPromise) {
                        prizeDiv.classList.remove('opacity-0');
                        fetchPromise = completeGame('KAZIKAZAN', true, game.oyunToken, []).then(data => {
                            resultFetched = true;
                            resultData = data;
                            loadDiv.classList.add('hidden');
                            textDiv.classList.remove('hidden');
                            
                            if (data.kazandiMi) {
                                textDiv.innerHTML = `🎁<br>${data.odulTanim}`;
                                textDiv.classList.add('text-primary');
                            } else {
                                textDiv.innerHTML = `😢<br>${L('empty', 'Boş')}`;
                                textDiv.classList.add('text-on-surface-variant');
                            }
                        }).catch(err => {
                            console.error(err);
                            resultFetched = true;
                            loadDiv.classList.add('hidden');
                            textDiv.classList.remove('hidden');
                            textDiv.innerHTML = `⚠️<br>${L('errorWord', 'Hata')}`;
                        });
                    }
                };

                const getPos = (e) => {
                    const rect = canvas.getBoundingClientRect();
                    const clientX = e.touches ? e.touches[0].clientX : e.clientX;
                    const clientY = e.touches ? e.touches[0].clientY : e.clientY;
                    return {
                        x: (clientX - rect.left) * (canvas.width / rect.width),
                        y: (clientY - rect.top) * (canvas.height / rect.height)
                    };
                };

                const startDraw = (e) => {
                    if (finished) return;
                    isDrawing = true;
                    fetchResultOnce();
                    draw(e);
                };

                const endDraw = () => {
                    isDrawing = false;
                    checkPercentage();
                };

                const draw = (e) => {
                    if (!isDrawing || finished) return;
                    e.preventDefault();
                    const pos = getPos(e);
                    ctx.globalCompositeOperation = 'destination-out';
                    ctx.beginPath();
                    ctx.arc(pos.x, pos.y, 20, 0, Math.PI * 2);
                    ctx.fill();
                };

                const checkPercentage = () => {
                    if (!resultFetched) return;
                    const pixels = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
                    let transparent = 0;
                    for (let i = 3; i < pixels.length; i += 4) {
                        if (pixels[i] < 50) transparent++;
                    }
                    const percent = transparent / (canvas.width * canvas.height);
                    if (percent > 0.45 && !finished) {
                        finished = true;
                        ctx.clearRect(0, 0, canvas.width, canvas.height);
                        canvas.style.pointerEvents = 'none';
                        if (resultData) {
                            const html = resultData.kazandiMi 
                                 ? `🎉 ${resultData.message}<br><small>${L('newTotal', 'Yeni tutar')}: ${resultData.yeniTutar.toFixed(2)} ₺</small>`
                                 : `😢 ${resultData.message}`;
                            showResultWithCountdown(resultMsg, html, 6, () => { closeGameModal(); fetchOrders(); });
                        }
                    }
                };

                canvas.addEventListener('mousedown', startDraw);
                canvas.addEventListener('mousemove', draw);
                canvas.addEventListener('mouseup', endDraw);
                canvas.addEventListener('mouseleave', endDraw);

                canvas.addEventListener('touchstart', startDraw, { passive: false });
                canvas.addEventListener('touchmove', draw, { passive: false });
                canvas.addEventListener('touchend', endDraw);
            }
        }

        function showToast(msg, isError) {
            const el = document.getElementById('toastBildirim');
            el.className = 'px-6 py-3 rounded-full shadow-2xl font-headline font-semibold text-sm flex items-center gap-2 transition-all duration-300 text-white ' + (isError ? 'bg-error' : 'bg-primary');
            document.getElementById('toastMesaj').textContent = msg;
            el.style.opacity = '1'; el.style.transform = 'translateY(0)';
            setTimeout(() => { el.style.opacity = '0'; el.style.transform = 'translateY(16px)'; }, 3000);
        }

})();
