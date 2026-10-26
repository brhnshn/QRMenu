        let dangerThresholdMs = 5 * 60 * 1000;
        let pinCriticalTop = true;
        let showCustomerNotes = true;
        let compactCards = false;
        let criticalPulse = true;
        let lastKdsSortSignature = '';

        function loadRuntimeSettings() {
            try {
                const criticalMinutes = parseInt(localStorage.getItem('kdsCriticalMinutes') || '', 10);
                if (Number.isFinite(criticalMinutes) && criticalMinutes >= 1 && criticalMinutes <= 60) {
                    dangerThresholdMs = criticalMinutes * 60 * 1000;
                } else {
                    dangerThresholdMs = 5 * 60 * 1000;
                }

                const pinTopRaw = localStorage.getItem('kdsPinCriticalTop');
                pinCriticalTop = pinTopRaw === null ? true : pinTopRaw === '1';

                const showNotesRaw = localStorage.getItem('kdsShowCustomerNotes');
                showCustomerNotes = showNotesRaw === null ? true : showNotesRaw === '1';

                const compactRaw = localStorage.getItem('kdsCompactCards');
                compactCards = compactRaw === null ? false : compactRaw === '1';

                const pulseRaw = localStorage.getItem('kdsCriticalPulse');
                criticalPulse = pulseRaw === null ? true : pulseRaw === '1';
                return;
            } catch (e) {
                // localStorage erişilemiyorsa varsayılan süre kullan
            }

            dangerThresholdMs = 5 * 60 * 1000;
            pinCriticalTop = true;
            showCustomerNotes = true;
            compactCards = false;
            criticalPulse = true;
        }

        function applyRuntimeUiSettings() {
            const root = document.getElementById('kdsPanelRoot');
            if (root) {
                root.classList.toggle('kds-compact-mode', compactCards);
            }

            document.querySelectorAll('.kds-customer-note').forEach(note => {
                note.style.display = showCustomerNotes ? '' : 'none';
            });
        }

        function getPreparedDetailIds() {
            try {
                const raw = localStorage.getItem('kdsPreparedDetails') || '[]';
                const parsed = JSON.parse(raw);
                return Array.isArray(parsed) ? new Set(parsed.map(String)) : new Set();
            } catch (e) {
                return new Set();
            }
        }

        function savePreparedDetailIds(ids) {
            try {
                localStorage.setItem('kdsPreparedDetails', JSON.stringify(Array.from(ids)));
            } catch (e) {
                // İşaretleme sadece mutfak ekranına yardımcı yerel bilgi; kaydedilemezse ekran yine çalışır.
            }
        }

        function setPreparedVisualState(row, isPrepared) {
            if (!row) {
                return;
            }

            row.classList.toggle('is-prepared', isPrepared);
            const button = row.querySelector('[data-prepared-toggle]');
            if (!button) {
                return;
            }

            button.classList.toggle('is-prepared', isPrepared);
            button.setAttribute('aria-pressed', isPrepared ? 'true' : 'false');

            const icon = button.querySelector('.material-symbols-outlined');
            if (icon) {
                icon.textContent = isPrepared ? 'check_circle' : 'radio_button_unchecked';
            }

            const label = button.querySelector('.kds-prepared-label');
            if (label) {
                label.textContent = isPrepared ? 'Hazırlandı' : 'Hazırlandı';
            }
        }

        function applyPreparedMarks(scope = document) {
            const preparedIds = getPreparedDetailIds();
            scope.querySelectorAll('[data-detail-id]').forEach(row => {
                const detailId = row.getAttribute('data-detail-id');
                setPreparedVisualState(row, preparedIds.has(String(detailId)));
            });
        }

        async function submitAutoReadyIfAllPrepared(scope) {
            const receiptShell = scope?.closest?.('.kds-receipt-shell') || document.querySelector('#kdsReceiptBody .kds-receipt-shell');
            if (!receiptShell || receiptShell.dataset.autoReadySubmitted === '1') {
                return;
            }

            const itemRows = Array.from(receiptShell.querySelectorAll('[data-detail-id]'));
            if (!itemRows.length || itemRows.some(row => !row.classList.contains('is-prepared'))) {
                return;
            }

            const siparisId = parseInt(receiptShell.getAttribute('data-receipt-order-id') || '', 10);
            if (!Number.isFinite(siparisId)) {
                return;
            }

            receiptShell.dataset.autoReadySubmitted = '1';
            const status = receiptShell.getAttribute('data-receipt-status');

            if (status === 'Onaylandi') {
                const hazirlaniyorOk = await durumGuncelle(siparisId, 'Hazirlaniyor', false);
                if (!hazirlaniyorOk) {
                    receiptShell.dataset.autoReadySubmitted = '0';
                    return;
                }
            }

            const hazirOk = await durumGuncelle(siparisId, 'Hazir');
            if (hazirOk) {
                closeReceipt();
            } else {
                receiptShell.dataset.autoReadySubmitted = '0';
            }
        }

        function openReceipt(receiptId) {
            const modal = document.getElementById('kdsReceiptModal');
            const body = document.getElementById('kdsReceiptBody');
            const template = document.getElementById(`kdsReceipt-${receiptId}`);

            if (!modal || !body || !template) {
                return;
            }

            body.innerHTML = '';
            body.appendChild(template.content.cloneNode(true));
            applyPreparedMarks(body);
            const receipt = body.querySelector('.kds-receipt-shell');
            if (receipt) {
                setKdsCancelMode(getKdsCancelMode(), receipt);
                updateKdsReceiptSelection(receipt);
            }
            modal.classList.add('is-open');
            document.body.classList.add('kds-modal-open');
            document.body.style.overflow = 'hidden';
        }

        function closeReceipt() {
            const modal = document.getElementById('kdsReceiptModal');
            const body = document.getElementById('kdsReceiptBody');

            if (modal) {
                modal.classList.remove('is-open');
            }

            if (body) {
                body.innerHTML = '';
            }

            document.body.classList.remove('kds-modal-open');
            document.body.style.overflow = '';
        }

        function updateTimers() {
            const cards = document.querySelectorAll('.kds-ticket-card');
            const now = new Date();

            cards.forEach(card => {
                const tarihStr = card.getAttribute('data-tarih');
                const pDate = new Date(tarihStr);
                const diffMs = now - pDate;
                if (diffMs < 0) {
                    return;
                }

                const diffSec = Math.floor(diffMs / 1000);
                const mins = Math.floor(diffSec / 60);
                const secs = diffSec % 60;

                const timerText = card.querySelector('.timer-text');
                if (timerText) {
                    timerText.innerText = `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
                }

                const isCritical = diffMs > dangerThresholdMs;
                card.dataset.critical = isCritical ? '1' : '0';
                card.classList.toggle('kds-danger', criticalPulse && isCritical);
            });

            const sortSignature = Array.from(cards)
                .map(card => `${card.getAttribute('data-id') || ''}:${card.dataset.critical || '0'}`)
                .join('|');
            const shouldSort = sortSignature !== lastKdsSortSignature
                && pinCriticalTop
                && window.scrollY < 20
                && !document.body.classList.contains('kds-modal-open');
            if (shouldSort) {
                lastKdsSortSignature = sortSignature;
                sortKdsCardsByPriority();
            }
        }

        function sortKdsCardsByPriority() {
            const grid = document.getElementById('kdsGrid');
            if (!grid) {
                return;
            }

            const cards = Array.from(grid.querySelectorAll('.kds-ticket-card'));
            if (!cards.length) {
                return;
            }

            cards.sort((a, b) => {
                const aCritical = a.dataset.critical === '1' ? 1 : 0;
                const bCritical = b.dataset.critical === '1' ? 1 : 0;

                if (pinCriticalTop && aCritical !== bCritical) {
                    // Kritik biletler üstte kalır.
                    return bCritical - aCritical;
                }

                const aTime = Date.parse(a.dataset.tarih || '');
                const bTime = Date.parse(b.dataset.tarih || '');
                const safeATime = Number.isFinite(aTime) ? aTime : 0;
                const safeBTime = Number.isFinite(bTime) ? bTime : 0;

                // Aynı öncelikte eski siparişler üstte kalır.
                return safeATime - safeBTime;
            });

            cards.forEach((card) => {
                grid.appendChild(card);
            });
        }

        setInterval(updateTimers, 1000);
        loadRuntimeSettings();
        applyRuntimeUiSettings();
        updateTimers();

        document.addEventListener('click', function (event) {

            const cancelButton = event.target.closest('[data-cancel-selected]');
            if (cancelButton) {
                event.preventDefault();
                if (!cancelButton.disabled) {
                    iptalSeciliDetaylar();
                }
                return;
            }

            const preparedButton = event.target.closest('[data-prepared-toggle]');
            if (preparedButton) {
                event.preventDefault();
                const detailId = preparedButton.getAttribute('data-prepared-toggle');
                const row = preparedButton.closest('[data-detail-id]');
                const preparedIds = getPreparedDetailIds();
                const prepared = preparedIds.has(String(detailId));

                if (prepared) {
                    preparedIds.delete(String(detailId));
                } else {
                    preparedIds.add(String(detailId));
                }

                savePreparedDetailIds(preparedIds);
                setPreparedVisualState(row, !prepared);
                submitAutoReadyIfAllPrepared(row);
                return;
            }

            // Eğer fiş içindeki herhangi bir yere tıklanmışsa, küçük kutucukla uğraşılmasın; kartın herhangi bir yeri seçimi değiştirsin
            const receiptItem = event.target.closest('.kds-receipt-item');
            if (receiptItem) {
                // Eğer tıklama interaktif bir öğe üzerindeyse (input, select, button, a, label) geri dön
                if (event.target.closest('input,select,button,a,label')) {
                    return;
                }

                const checkbox = receiptItem.querySelector('.kds-cancel-input');
                if (checkbox && !checkbox.disabled) {
                    checkbox.checked = !checkbox.checked;
                    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
                }

                return;
            }

            const receiptButton = event.target.closest('[data-receipt-id]');
            if (receiptButton) {
                event.preventDefault();
                openReceipt(receiptButton.dataset.receiptId);
                return;
            }

            const modal = document.getElementById('kdsReceiptModal');
            if (modal && event.target === modal) {
                closeReceipt();
            }
        });

        document.addEventListener('change', function (event) {
            const cancelInput = event.target.closest('.kds-cancel-input');
            if (!cancelInput) {
                return;
            }

            const receipt = cancelInput.closest('.kds-receipt-shell');
            if (receipt) {
                updateKdsReceiptSelection(receipt);
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                closeReceipt();
            }
        });

        let refreshTimer = null;
        let lastRefreshAt = 0;
        let refreshInFlight = false;

        async function softRefreshKdsPanel() {
            if (refreshInFlight) {
                return;
            }

            refreshInFlight = true;
            try {
                const res = await fetch(window.location.pathname, {
                    cache: 'no-store',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                if (!res.ok) {
                    throw new Error('KDS verisi alınamadı.');
                }

                const html = await res.text();
                const doc = new DOMParser().parseFromString(html, 'text/html');
                const yeniPanel = doc.getElementById('kdsPanelRoot');
                const mevcutPanel = document.getElementById('kdsPanelRoot');

                if (yeniPanel && mevcutPanel) {
                    mevcutPanel.innerHTML = yeniPanel.innerHTML;
                    closeReceipt();
                    loadRuntimeSettings();
                    applyRuntimeUiSettings();
                    updateTimers();
                }
            } catch (err) {
                console.error(err);
                console.warn('Mutfak paneli güncellenemedi, mevcut içerik korunuyor.');
            } finally {
                refreshInFlight = false;
            }
        }

        function scheduleSoftRefresh(delayMs = 90) {
            const minIntervalMs = 150;
            const elapsed = Date.now() - lastRefreshAt;
            const waitMs = Math.max(delayMs, minIntervalMs - elapsed);

            if (refreshTimer) {
                return;
            }

            refreshTimer = setTimeout(async () => {
                refreshTimer = null;
                lastRefreshAt = Date.now();
                await softRefreshKdsPanel();
            }, Math.max(0, waitMs));
        }

        const inFlightDurumGuncellemeleri = new Set();

        async function durumGuncelleClick(button, siparisId, durum, closeAfter = false) {
            if (inFlightDurumGuncellemeleri.has(siparisId)) {
                return;
            }

            const originalHtml = button ? button.innerHTML : null;
            if (button) {
                button.disabled = true;
                button.classList.add('opacity-80', 'cursor-not-allowed');
                button.innerHTML = '<span class="kds-spinner" aria-hidden="true"></span><span>İşleniyor</span>';
            }

            const ok = await durumGuncelle(siparisId, durum, true);

            if (button && !ok) {
                button.disabled = false;
                button.classList.remove('opacity-80', 'cursor-not-allowed');
                if (originalHtml !== null) {
                    button.innerHTML = originalHtml;
                }
            }

            if (ok && closeAfter) {
                closeReceipt();
            }
        }

        const kdsCancelModeKey = 'kdsCancelMode';

        function getKdsCancelMode() {
            return 'multi';
        }

        function setKdsCancelMode(mode, receipt) {
            // Artık sadece çoklu seçim var
            if (!receipt) return;
            receipt.dataset.cancelMode = 'multi';
            updateKdsReceiptSelection(receipt);
        }

        function updateKdsReceiptSelection(receipt) {
            if (!receipt) {
                return;
            }

            let selectedCount = 0;
            receipt.querySelectorAll('.kds-receipt-item').forEach(row => {
                const checked = row.querySelector('.kds-cancel-input')?.checked;
                row.classList.toggle('is-selected', Boolean(checked));
                if (checked) {
                    selectedCount += 1;
                }
            });

            const cancelBtn = receipt.querySelector('[data-cancel-selected]');
            const enabled = cancelBtn?.getAttribute('data-cancel-enabled') !== '0';
            if (cancelBtn) {
                cancelBtn.disabled = !enabled || selectedCount === 0;
            }
        }

        function collectKdsCancelDetails(receipt) {
            if (!receipt) {
                return [];
            }

            const selectionMap = new Map();
            receipt.querySelectorAll('.kds-cancel-input:checked').forEach(input => {
                const detailId = Number.parseInt(input.value, 10);
                if (Number.isFinite(detailId)) {
                    selectionMap.set(detailId, (selectionMap.get(detailId) || 0) + 1);
                }
            });

            const result = [];
            selectionMap.forEach((adet, siparisDetayId) => {
                result.push({ siparisDetayId, adet });
            });
            return result;
        }

        async function iptalSeciliDetaylar() {
            const receipt = document.querySelector('#kdsReceiptBody .kds-receipt-shell');
            if (!receipt) {
                return;
            }

            const detaylar = collectKdsCancelDetails(receipt);
            if (!detaylar.length) {
                return;
            }

            const onay = confirm('Seçilen kalemler iptal edilsin mi?');
            if (!onay) {
                return;
            }

            const cancelBtn = receipt.querySelector('[data-cancel-selected]');
            const spinner = receipt.querySelector('#cancelSpinner');
            const originalHtml = cancelBtn ? cancelBtn.querySelector('span:not(.kds-btn-spinner)')?.innerText : 'Seçilenleri İptal Et';

            if (cancelBtn) {
                cancelBtn.disabled = true;
                cancelBtn.classList.add('is-busy');
                if (spinner) spinner.classList.remove('hidden');
                const label = cancelBtn.querySelector('span:not(.kds-btn-spinner)');
                if (label) label.innerText = 'İşleniyor...';
            }

            try {
                const res = await fetch('/Mutfak/SiparisDetayIptal', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ detaylar })
                });

                const data = await res.json();
                if (data.success) {
                    closeReceipt();
                    scheduleSoftRefresh(120);
                    return;
                }

                alert('İptal sırasında hata: ' + (data.message || 'Bilinmeyen hata'));
            } catch (err) {
                console.error(err);
                alert('İptal sırasında bağlantı hatası oluştu.');
            } finally {
                if (cancelBtn) {
                    cancelBtn.disabled = false;
                    cancelBtn.classList.remove('is-busy');
                    if (spinner) spinner.classList.add('hidden');
                    const label = cancelBtn.querySelector('span:not(.kds-btn-spinner)');
                    if (label) label.innerText = originalHtml;
                }
                updateKdsReceiptSelection(receipt);
            }
        }

        async function durumGuncelle(siparisId, durum, refresh = true) {
            if (inFlightDurumGuncellemeleri.has(siparisId)) {
                return false;
            }

            inFlightDurumGuncellemeleri.add(siparisId);
            try {
                const payload = { yeniDurum: durum };
                const res = await fetch(`/Mutfak/DurumGuncelle/${siparisId}`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                const data = await res.json();

                if (data.success) {
                    if (refresh) {
                        scheduleSoftRefresh(100);
                    }
                    return true;
                } else {
                    alert('Durum güncellenirken hata: ' + data.message);
                    return false;
                }
            } catch (err) {
                console.error(err);
                alert('Durum güncellenirken beklenmeyen bir hata oluştu.');
                return false;
            } finally {
                inFlightDurumGuncellemeleri.delete(siparisId);
            }
        }

        function playBell() {
            try {
                if (localStorage.getItem('kdsEnableSound') === '0') {
                    return;
                }
            } catch (e) {
                // localStorage erişilemiyorsa ses varsayılan açık kalır
            }

            const bell = document.getElementById('bellSound');
            if (bell) {
                bell.play().catch(e => console.log('Ses çalma hatası:', e));
            }
        }

        function buildConnection(useFallbackTransports) {
            const options = useFallbackTransports
                ? { transport: signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling }
                : undefined;

            return new signalR.HubConnectionBuilder()
                .withUrl('/hubs/order', options)
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Error)
                .build();
        }

        function registerConnectionHandlers(conn) {
            conn.on('SiparisGeldi', function () {
                playBell();
                scheduleSoftRefresh(100);
            });

            conn.on('SiparisEklendi', function () {
                playBell();
                scheduleSoftRefresh(100);
            });

            conn.on('SiparisGuncellendi', function () {
                scheduleSoftRefresh(80);
            });
        }

        let connection = buildConnection(false);
        registerConnectionHandlers(connection);

        async function startConnection(conn, allowFallback) {
            try {
                await conn.start();
                console.log('KDS bağlantısı başarılı.');
            } catch (err) {
                console.error('SignalR KDS hatası:', err);
                if (!allowFallback) return;

                try {
                    await conn.stop();
                } catch (stopErr) {
                    console.warn('SignalR kapatılırken hata:', stopErr);
                }

                connection = buildConnection(true);
                registerConnectionHandlers(connection);
                await startConnection(connection, false);
            }
        }

        startConnection(connection, true);
