    function mesaj(text, tip) {
        var bgColor = tip === 'success' ? 'bg-emerald-500' : (tip === 'danger' ? 'bg-red-500' : 'bg-primary');
        document.getElementById('mesaj').innerHTML = '<div class="fixed bottom-5 right-5 ' + bgColor + ' text-white px-6 py-3 rounded-xl shadow-xl font-bold z-[9999] animate-[bounce_1s_ease-in-out]">' + text + '</div>';
        setTimeout(function() { document.getElementById('mesaj').innerHTML = ''; }, 3000);
    }

    function filterTable() {
        var katId = document.getElementById('filterKategori').value;
        var search = document.getElementById('filterSearch').value.toLowerCase();
        var durum = document.getElementById('filterDurum').value;

        // Desktop rows
        document.querySelectorAll('.urun-row').forEach(function(tr) {
            var matchKat = !katId || tr.dataset.kategori === katId;
            var matchSearch = !search || tr.dataset.ad.includes(search);
            var matchDurum = !durum || tr.dataset.durum === durum;
            tr.style.display = (matchKat && matchSearch && matchDurum) ? '' : 'none';
        });

        // Mobile cards
        document.querySelectorAll('.urun-row-mobile').forEach(function(div) {
            var matchKat = !katId || div.dataset.kategori === katId;
            var matchSearch = !search || div.dataset.ad.includes(search);
            var matchDurum = !durum || div.dataset.durum === durum;
            div.style.display = (matchKat && matchSearch && matchDurum) ? 'flex' : 'none';
        });
    }

    // --- KATEGORİ ---
    function kategoriModal(id, ad, adEN, siraNo) {
        document.getElementById('kategoriEditId').value = id || 0;
        document.getElementById('kategoriAd').value = ad || '';
        document.getElementById('kategoriAdEN').value = adEN || '';
        document.getElementById('kategoriSiraNo').value = siraNo || 0;
        document.getElementById('kategoriModalTitle').innerHTML = id ? 'Kategori Düzenle' : 'Kategori Ekle';
        new bootstrap.Modal(document.getElementById('kategoriModal')).show();
    }

    async function kategoriKaydet() {
        var id = parseInt(document.getElementById('kategoriEditId').value);
        var ad = document.getElementById('kategoriAd').value.trim();
        if (!ad) { mesaj('Kategori adı boş olamaz.', 'danger'); return; }

        var payload = { ad: ad, adEN: document.getElementById('kategoriAdEN').value.trim() || null, siraNo: parseInt(document.getElementById('kategoriSiraNo').value) || 0 };
        var url = id > 0 ? '/admin/kategori-guncelle/' + id : '/admin/kategori-ekle';
        var res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
        var data = await res.json();
        
        if (data.success) {
            mesaj(id > 0 ? 'Kategori güncellendi.' : 'Kategori eklendi.', 'success');
            setTimeout(function() { location.reload(); }, 500);
        } else { mesaj(data.message, 'danger'); }
    }

    async function kategoriSil(id, ad) {
        const result = await window.SavorAlert.fire({
            title: 'Emin misiniz?',
            text: '"' + ad + '" kategorisi silinsin mi?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Evet, Sil!',
            cancelButtonText: 'İptal'
        });
        if (!result.isConfirmed) return;
        var res = await fetch('/admin/kategori-sil/' + id, { method: 'POST' });
        var data = await res.json();
        if (data.success) {
            mesaj('Kategori silindi.', 'success');
            setTimeout(function() { location.reload(); }, 500);
        } else { mesaj(data.message, 'warning'); }
    }

    // --- ÜRÜN ---
    function urunModal() {
        document.getElementById('urunEditId').value = 0;
        document.getElementById('urunForm').reset();
        document.getElementById('urunAktif').checked = true;
        document.getElementById('urunStok').value = 0;
        document.getElementById('gorselPreview').style.display = 'none';
        document.getElementById('currentGorselPath').innerText = '';
        document.getElementById('urunModalTitle').innerHTML = 'Yeni Ürün Ekle';
        new bootstrap.Modal(document.getElementById('urunModal')).show();
    }

    async function urunDuzenle(id) {
        var res = await fetch('/admin/urun-detay/' + id);
        var data = await res.json();
        if (!data.success) { mesaj(data.message, 'danger'); return; }
        var u = data.urun;

        document.getElementById('urunEditId').value = u.id;
        document.getElementById('urunAd').value = u.ad;
        document.getElementById('urunAdEN').value = u.adEN || '';
        document.getElementById('urunAciklama').value = u.aciklama || '';
        document.getElementById('urunAciklamaEN').value = u.aciklamaEN || '';
        document.getElementById('urunFiyat').value = u.fiyat;
        document.getElementById('urunStok').value = u.stokAdet || 0;
        document.getElementById('urunKategori').value = u.kategoriId;
        document.getElementById('urunPopuler').checked = u.populerMi;
        document.getElementById('urunAktif').checked = u.aktifMi;
        document.getElementById('urunKalori').value = u.kalori || '';
        document.getElementById('urunGorsel').value = '';

        var preview = document.getElementById('gorselPreview');
        var pathDiv = document.getElementById('currentGorselPath');
        if (u.gorselUrl) { 
            preview.src = u.gorselUrl; 
            preview.style.display = 'block';
            pathDiv.innerText = 'Mevcut: ' + u.gorselUrl.split('/').pop();
        }
        else { 
            preview.style.display = 'none'; 
            pathDiv.innerText = '';
        }

        document.getElementById('urunModalTitle').innerHTML = 'Ürün Düzenle: ' + u.ad;
        new bootstrap.Modal(document.getElementById('urunModal')).show();
    }

    async function urunKaydet() {
        var id = parseInt(document.getElementById('urunEditId').value);
        var form = new FormData();
        form.append('Ad', document.getElementById('urunAd').value);
        form.append('AdEN', document.getElementById('urunAdEN').value);
        form.append('Aciklama', document.getElementById('urunAciklama').value);
        form.append('AciklamaEN', document.getElementById('urunAciklamaEN').value);
        form.append('Fiyat', document.getElementById('urunFiyat').value);
        form.append('StokAdet', document.getElementById('urunStok').value || '0');
        form.append('KategoriId', document.getElementById('urunKategori').value);
        form.append('PopulerMi', document.getElementById('urunPopuler').checked);
        form.append('AktifMi', document.getElementById('urunAktif').checked);
        var kaloriVal = document.getElementById('urunKalori').value;
        if (kaloriVal) form.append('Kalori', kaloriVal);

        var gorselInput = document.getElementById('urunGorsel');
        if (gorselInput.files.length > 0) form.append('Gorsel', gorselInput.files[0]);

        var url = id > 0 ? '/admin/urun-guncelle/' + id : '/admin/urun-ekle';
        var res = await fetch(url, { method: 'POST', body: form });
        var data = await res.json();

        if (data.success) {
            mesaj(id > 0 ? 'Ürün güncellendi.' : 'Ürün eklendi.', 'success');
            setTimeout(function() { location.reload(); }, 500);
        } else { mesaj(data.message, 'danger'); }
    }

    async function urunSil(id, ad) {
        const result = await window.SavorAlert.fire({
            title: 'Emin misiniz?',
            text: '"' + ad + '" ürünü silinsin mi?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Evet, Sil!',
            cancelButtonText: 'İptal'
        });
        if (!result.isConfirmed) return;
        var res = await fetch('/admin/urun-sil/' + id, { method: 'POST' });
        var data = await res.json();
        if (data.success) {
            mesaj(data.message || 'Ürün silindi.', 'success');
            setTimeout(function() { location.reload(); }, 500);
        } else { mesaj(data.message, 'danger'); }
    }

    async function toggleUrun(id) {
        var res = await fetch('/admin/urun-toggle/' + id, { method: 'POST' });
        var data = await res.json();
        if (data.success) {
            mesaj('Durum güncellendi.', 'success');
            setTimeout(function() { location.reload(); }, 300);
        }
    }

    function previewImage(input) {
        var preview = document.getElementById('gorselPreview');
        if (input.files && input.files[0]) {
            var reader = new FileReader();
            reader.onload = function(e) { preview.src = e.target.result; preview.style.display = 'block'; };
            reader.readAsDataURL(input.files[0]);
        } else { preview.style.display = 'none'; }
    }

    // --- TAŞIMA ---
    function urunTasiModalAc() {
        document.getElementById('urunTasiForm').reset();
        document.getElementById('tasinacakUrunlerList').innerHTML = '<span class="text-sm text-stone-400">Önce kaynak kategori seçin.</span>';
        new bootstrap.Modal(document.getElementById('urunTasiModal')).show();
    }

    function urunTasiListeYenile() {
        var kaynakKategoriId = document.getElementById('kaynakKategoriId').value;
        var list = document.getElementById('tasinacakUrunlerList');
        if (!kaynakKategoriId) { list.innerHTML = '<span class="text-sm text-stone-400">Önce kaynak kategori seçin.</span>'; return; }

        var urunler = Array.from(document.querySelectorAll('.urun-row'))
            .filter(function(tr) { return tr.dataset.kategori === kaynakKategoriId; })
            .map(function(tr) { return { id: tr.dataset.id, ad: tr.dataset.ad }; });

        if (!urunler.length) { list.innerHTML = '<span class="text-sm text-stone-400">Bu kategoride taşınacak ürün bulunamadı.</span>'; return; }

        list.innerHTML = urunler.map(function(u) {
            return '<div class="flex items-center gap-2 mb-2">' +
                '<input type="checkbox" class="w-4 h-4 rounded text-primary focus:ring-primary border-stone-300 tasinacak-urun-check" value="' + u.id + '" id="tasiUrun_' + u.id + '">' +
                '<label class="text-sm text-stone-700 cursor-pointer" for="tasiUrun_' + u.id + '">' + u.ad + '</label>' +
                '</div>';
        }).join('');
    }

    async function urunTasiKaydet() {
        var kaynakKategoriId = document.getElementById('kaynakKategoriId').value;
        var kategoriId = document.getElementById('hedefKategoriId').value;
        var secilenUrunIds = Array.from(document.querySelectorAll('.tasinacak-urun-check:checked')).map(function(cb) { return parseInt(cb.value); });

        if (!kaynakKategoriId || !kategoriId) { mesaj('Kategori seçimi zorunlu.', 'warning'); return; }
        if (parseInt(kaynakKategoriId) === parseInt(kategoriId)) { mesaj('Aynı kategoriye taşınamaz.', 'warning'); return; }
        if (!secilenUrunIds.length) { mesaj('Ürün seçin.', 'warning'); return; }

        var res = await fetch('/admin/urun-tasi', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ UrunIds: secilenUrunIds, YeniKategoriId: parseInt(kategoriId) }) });
        var data = await res.json();
        if (data.success) {
            mesaj(secilenUrunIds.length + ' ürün taşındı.', 'success');
            setTimeout(function() { location.reload(); }, 500);
        } else { mesaj(data.message, 'danger'); }
    }

    // --- OPSİYON ---
    var currentProductGroups = [];
    async function opsiyonModal(urunId, urunAd) {
        document.getElementById('opsiyonUrunId').value = urunId;
        document.getElementById('opsiyonModalSubtitle').innerText = urunAd + ' için ek seçenekler';
        
        resetGrupSecim();
        document.getElementById('yeniOpsiyonAd').value = '';
        document.getElementById('yeniOpsiyonAdEN').value = '';
        document.getElementById('yeniOpsiyonFiyat').value = 0;
        document.getElementById('zorunluToggleOn').checked = true;

        await opsiyonListeYenile(urunId);
        new bootstrap.Modal(document.getElementById('opsiyonModal')).show();
    }

    function grupSecildi(el) {
        var val = el.value;
        var yeniArea = document.getElementById('yeniGrupArea');
        var grupInput = document.getElementById('yeniOpsiyonGrup');
        var grupENInput = document.getElementById('yeniOpsiyonGrupEN');
        var btnReset = document.getElementById('btnGrupReset');

        if (val === 'NEW') {
            yeniArea.classList.remove('hidden');
            grupInput.value = '';
            grupENInput.value = '';
            grupENInput.readOnly = false;
            btnReset.classList.remove('hidden');
        } else if (val !== '') {
            yeniArea.classList.add('hidden');
            var found = currentProductGroups.find(g => g.grup === val);
            grupInput.value = val;
            grupENInput.value = found ? (found.grupEN || '') : '';
            grupENInput.readOnly = true;
            btnReset.classList.remove('hidden');
        } else {
            resetGrupSecim();
        }
    }

    function resetGrupSecim() {
        document.getElementById('grupSecici').value = '';
        document.getElementById('yeniGrupArea').classList.add('hidden');
        document.getElementById('yeniOpsiyonGrup').value = '';
        document.getElementById('yeniOpsiyonGrupEN').value = '';
        document.getElementById('yeniOpsiyonGrupEN').readOnly = false;
        document.getElementById('btnGrupReset').classList.add('hidden');
    }

    async function opsiyonListeYenile(urunId) {
        var res = await fetch('/admin/urun-detay/' + urunId);
        var data = await res.json();
        var listDiv = document.getElementById('opsiyonList');

        if (!data.success || !data.urun.opsiyonlar || data.urun.opsiyonlar.length === 0) {
            listDiv.innerHTML = '<div class="text-center py-6 text-stone-400 text-sm">Bu ürüne henüz opsiyon eklenmemiş.</div>';
            return;
        }

        var gruplar = {};
        currentProductGroups = [];
        data.urun.opsiyonlar.forEach(function(o) {
            if (!gruplar[o.grup]) {
                gruplar[o.grup] = { adEN: o.grupEN, zorunlu: o.zorunlu, opsiyonlar: [] };
                currentProductGroups.push({ grup: o.grup, grupEN: o.grupEN });
            }
            gruplar[o.grup].opsiyonlar.push(o);
        });

        // Grup seçiciyi güncelle
        var sel = document.getElementById('grupSecici');
        sel.innerHTML = '<option value="">Seçiniz...</option>' + 
                       currentProductGroups.map(g => '<option value="' + g.grup + '">' + g.grup + '</option>').join('') +
                       '<option value="NEW">+ Yeni Grup Ekle</option>';

        var html = '';
        for(var g in gruplar) {
            var dt = gruplar[g];
            html += '<div class="bg-surface-container-lowest border border-stone-200 rounded-2xl p-6">' +
                '<div class="flex justify-between items-start mb-4">' +
                '<div><h4 class="font-bold text-stone-900">' + g + (dt.adEN ? ' <span class="text-xs text-stone-400">(' + dt.adEN + ')</span>' : '') + '</h4>' +
                '<p class="text-xs text-stone-400">' + (dt.zorunlu ? 'Zorunlu' : 'Opsiyonel') + '</p></div></div>' +
                '<div class="flex flex-col gap-2">';
            
            dt.opsiyonlar.forEach(function(o) {
                html += '<div class="flex justify-between items-center bg-white border border-stone-100 rounded-xl px-4 py-2">' +
                    '<div class="flex flex-col"><span class="text-sm font-bold text-stone-700">' + o.ad + '</span>' +
                    (o.adEN ? '<span class="text-[10px] text-stone-400">' + o.adEN + '</span>' : '') + '</div>' +
                    '<div class="flex items-center gap-4">' +
                    (o.ekFiyat > 0 ? '<span class="text-sm font-bold text-emerald-600">+₺' + o.ekFiyat.toFixed(2) + '</span>' : '<span class="text-xs text-stone-400">Ücretsiz</span>') +
                    '<button class="text-error hover:text-red-700 p-1" onclick="opsiyonSil(' + o.id + ')" title="Sil"><span class="material-symbols-outlined text-sm">delete</span></button>' +
                    '</div></div>';
            });
            html += '</div></div>';
        }
        listDiv.innerHTML = html;
    }

    async function opsiyonEkle() {
        var urunId = parseInt(document.getElementById('opsiyonUrunId').value);
        var grup = document.getElementById('yeniOpsiyonGrup').value.trim();
        var ad = document.getElementById('yeniOpsiyonAd').value.trim();
        if (!grup || !ad) { mesaj('TR Grup ve Ad zorunlu.', 'danger'); return; }

        var payload = {
            urunId: urunId, ad: ad, adEN: document.getElementById('yeniOpsiyonAdEN').value.trim() || null,
            grup: grup, grupEN: document.getElementById('yeniOpsiyonGrupEN').value.trim() || null,
            ekFiyat: parseFloat(document.getElementById('yeniOpsiyonFiyat').value) || 0,
            zorunlu: document.getElementById('zorunluToggleOn').checked
        };
        
        var res = await fetch('/admin/opsiyon-ekle', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
        var data = await res.json();
        if (data.success) {
            mesaj('Opsiyon eklendi.', 'success');
            document.getElementById('yeniOpsiyonAd').value = '';
            document.getElementById('yeniOpsiyonAdEN').value = '';
            document.getElementById('yeniOpsiyonFiyat').value = 0;
            await opsiyonListeYenile(urunId);
        } else { mesaj(data.message, 'warning'); }
    }

    async function opsiyonSil(id) {
        var urunId = parseInt(document.getElementById('opsiyonUrunId').value);
        var res = await fetch('/admin/opsiyon-sil/' + id, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ urunId: urunId }) });
        var data = await res.json();
        if (data.success) {
            mesaj('Opsiyon silindi.', 'success');
            await opsiyonListeYenile(urunId);
        } else { mesaj(data.message, 'danger'); }
    }

    // --- OTOMATİK ÇEVİRİ ---
    async function translateTrToEn(trText) {
        try {
            var url = 'https://translate.googleapis.com/translate_a/single?client=gtx&sl=tr&tl=en&dt=t&q=' + encodeURIComponent(trText);
            var res = await fetch(url);
            var data = await res.json();
            return data[0][0][0];
        } catch(e) {
            return null;
        }
    }

    function bindAutoTranslate(sourceId, targetId) {
        var el = document.getElementById(sourceId);
        if (!el) return;
        el.addEventListener('blur', async function() {
            var trText = this.value.trim();
            var enInput = document.getElementById(targetId);
            if (trText && enInput && !enInput.value.trim()) {
                var ceviri = await translateTrToEn(trText);
                if (ceviri) enInput.value = ceviri;
            }
        });
    }

    function initAutoTranslate() {
        bindAutoTranslate('kategoriAd', 'kategoriAdEN');
        bindAutoTranslate('urunAd', 'urunAdEN');
        bindAutoTranslate('urunAciklama', 'urunAciklamaEN');
        bindAutoTranslate('yeniOpsiyonGrup', 'yeniOpsiyonGrupEN');
        bindAutoTranslate('yeniOpsiyonAd', 'yeniOpsiyonAdEN');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAutoTranslate);
    } else {
        initAutoTranslate();
    }
