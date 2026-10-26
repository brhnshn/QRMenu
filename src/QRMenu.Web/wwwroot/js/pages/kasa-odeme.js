        let seciliOdemeTipi = 'Nakit';

        function readDefaultPaymentType() {
            try {
                const stored = localStorage.getItem('kasaDefaultPayment');
                return stored === 'Kart' ? 'Kart' : 'Nakit';
            } catch (e) {
                return 'Nakit';
            }
        }

        function formatTl(value) {
            return Number(value || 0).toLocaleString('tr-TR', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            }) + ' TL';
        }

        function seciliOdemeTipiDegistir(tip) {
            seciliOdemeTipi = tip;

            const btnNakit = document.getElementById('btnTipNakit');
            const btnKart = document.getElementById('btnTipKart');

            if (btnNakit) {
                btnNakit.classList.toggle('is-active', tip === 'Nakit');
            }

            if (btnKart) {
                btnKart.classList.toggle('is-active', tip === 'Kart');
            }
        }

        function hesapla() {
            let toplam = 0;
            let secilenSayisi = 0;

            document.querySelectorAll('.urun-check').forEach(check => {
                const row = check.closest('.odeme-item-row');
                const fiyat = Number.parseFloat(check.getAttribute('data-fiyat') || '0');

                if (check.checked) {
                    secilenSayisi += 1;
                    toplam += Number.isFinite(fiyat) ? fiyat : 0;
                    if (row) {
                        row.classList.add('selected');
                    }
                } else if (row) {
                    row.classList.remove('selected');
                }
            });

            const araToplam = toplam / 1.10;
            const kdvToplam = toplam - araToplam;

            const secilenTutarTxt = document.getElementById('secilenTutarTxt');
            const araToplamTxt = document.getElementById('araToplamTxt');
            const kdvToplamTxt = document.getElementById('kdvToplamTxt');
            const indirimToplamTxt = document.getElementById('indirimToplamTxt');
            const seciliKalemSayisi = document.getElementById('seciliKalemSayisi');
            const btnTahsilat = document.getElementById('btnTahsilat');
            const btnIptal = document.getElementById('btnIptal');

            if (secilenTutarTxt) {
                secilenTutarTxt.innerText = formatTl(toplam);
            }
            if (araToplamTxt) {
                araToplamTxt.innerText = formatTl(araToplam);
            }
            if (kdvToplamTxt) {
                kdvToplamTxt.innerText = formatTl(kdvToplam);
            }
            if (indirimToplamTxt) {
                indirimToplamTxt.innerText = formatTl(0);
            }
            if (seciliKalemSayisi) {
                seciliKalemSayisi.innerText = String(secilenSayisi);
            }
            if (btnTahsilat) {
                btnTahsilat.disabled = secilenSayisi === 0;
            }
            if (btnIptal) {
                btnIptal.disabled = secilenSayisi === 0;
            }
        }

        function tumunuSec() {
            const checks = Array.from(document.querySelectorAll('.urun-check:not([disabled])'));
            if (!checks.length) {
                return;
            }

            const hepsiSecili = checks.every(c => c.checked);
            checks.forEach(c => {
                c.checked = !hepsiSecili;
            });

            hesapla();
        }

        async function softRefreshOdemePanel() {
            try {
                const res = await fetch(window.location.pathname, {
                    cache: 'no-store',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                if (!res.ok) {
                    throw new Error('Ödeme ekranı güncellenemedi.');
                }

                const html = await res.text();
                const doc = new DOMParser().parseFromString(html, 'text/html');
                const yeniKok = doc.querySelector('.odeme-root');
                const mevcutKok = document.querySelector('.odeme-root');

                if (yeniKok && mevcutKok) {
                    mevcutKok.innerHTML = yeniKok.innerHTML;
                }

                seciliOdemeTipi = readDefaultPaymentType();
                seciliOdemeTipiDegistir(seciliOdemeTipi);
                hesapla();
            } catch (err) {
                console.error(err);
                alert('Ekran güncellenemedi. Lütfen birazdan tekrar deneyin.');
            }
        }

        async function odemeYap() {
            const seciliChecks = Array.from(document.querySelectorAll('.urun-check:checked'));
            if (!seciliChecks.length) {
                return;
            }

            const btnTahsilat = document.getElementById('btnTahsilat');
            if (btnTahsilat) {
                btnTahsilat.disabled = true;
            }

            const payload = {
                masaId: Number.parseInt(document.querySelector('.odeme-root')?.dataset?.masaId || '0', 10),
                siparisDetayIds: seciliChecks.map(c => Number.parseInt(c.value, 10)),
                odemeTipi: seciliOdemeTipi
            };

            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            try {
                const response = await fetch('/Kasa/TahsilatYap', {
                    method: 'POST',
                    headers: { 
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token 
                    },
                    body: JSON.stringify(payload)
                });

                if (!response.ok) {
                    throw new Error(`HTTP Hata ${response.status}`);
                }

                const result = await response.json();
                if (result.success) {
                    console.log('Kasa işlemi başarılı: Tahsilat girişi yapıldı.');
                    await softRefreshOdemePanel();
                    return;
                }

                Swal.fire({
                    icon: 'error',
                    title: 'Tahsilat Hatası',
                    text: result.message || 'Bilinmeyen bir hata oluştu.'
                });
                hesapla();
            } catch (err) {
                console.error(err);
                Swal.fire({
                    icon: 'error',
                    title: 'Bağlantı Hatası',
                    text: 'Tahsilat işlemi sırasında sunucuyla iletişim kurulamadı veya CSRF doğrulaması başarısız oldu.'
                });
                hesapla();
            }
        }

        async function seciliKalemleriIptalEt() {
            const seciliChecks = Array.from(document.querySelectorAll('.urun-check:checked'));
            if (!seciliChecks.length) {
                return;
            }

            const onay = await Swal.fire({
                icon: 'warning',
                title: 'Seçilen kalemler iptal edilsin mi?',
                text: 'Bu işlem seçili kalemleri iptal durumuna alır.',
                showCancelButton: true,
                confirmButtonText: 'Evet, iptal et',
                cancelButtonText: 'Vazgeç'
            });

            if (!onay.isConfirmed) {
                return;
            }

            const btnIptal = document.getElementById('btnIptal');
            const spinner = document.getElementById('kasaIptalSpinner');
            const labelEl = btnIptal?.querySelector('span:not(.iptal-btn-spinner)');
            const originalText = labelEl ? labelEl.innerText : 'SEÇİLENLERİ İPTAL ET';

            if (btnIptal) {
                btnIptal.disabled = true;
                if (spinner) spinner.classList.remove('hidden');
                if (labelEl) labelEl.innerText = 'İPTAL EDİLİYOR...';
            }

            const selectionMap = new Map();
            seciliChecks.forEach(c => {
                const detailId = Number.parseInt(c.value, 10);
                if (Number.isFinite(detailId)) {
                    selectionMap.set(detailId, (selectionMap.get(detailId) || 0) + 1);
                }
            });

            const detaylar = [];
            selectionMap.forEach((adet, siparisDetayId) => {
                detaylar.push({ siparisDetayId, adet });
            });

            const payload = {
                masaId: Number.parseInt(document.querySelector('.odeme-root')?.dataset?.masaId || '0', 10),
                detaylar: detaylar
            };

            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            try {
                const response = await fetch('/Kasa/SeciliKalemleriIptalEt', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(payload)
                });

                if (!response.ok) {
                    throw new Error(`HTTP Hata ${response.status}`);
                }

                const result = await response.json();
                if (result.success) {
                    console.log('Kasa işlemi başarılı: Seçili sipariş kalemleri iptal edildi.');
                    await softRefreshOdemePanel();
                    return;
                }

                Swal.fire({
                    icon: 'error',
                    title: 'İptal Hatası',
                    text: result.message || 'Bilinmeyen bir hata oluştu.'
                });
            } catch (err) {
                console.error(err);
                Swal.fire({
                    icon: 'error',
                    title: 'Bağlantı Hatası',
                    text: 'İptal işlemi sırasında sunucuyla iletişim kurulamadı.'
                });
            } finally {
                if (btnIptal) {
                    btnIptal.disabled = false;
                    if (spinner) spinner.classList.add('hidden');
                    if (labelEl) labelEl.innerText = originalText;
                }
                hesapla();
            }
        }

        document.addEventListener('DOMContentLoaded', function () {
            seciliOdemeTipi = readDefaultPaymentType();
            seciliOdemeTipiDegistir(seciliOdemeTipi);
            hesapla();
        });
