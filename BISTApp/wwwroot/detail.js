// Hisse Detay Sayfası
const API_BASE_URL = window.location.origin;

const refreshBtn = document.getElementById('refreshBtn');
const loadingEl = document.getElementById('loading');
const titleEl = document.getElementById('title');
const candlesTableEl = document.getElementById('candlesTable');
const chartCanvas = document.getElementById('priceChart');

let chartInstance = null;

document.addEventListener('DOMContentLoaded', () => {
    refreshBtn.addEventListener('click', loadDetail);
    loadDetail();
});

function getSymbolFromQuery() {
    const params = new URLSearchParams(window.location.search);
    const symbol = (params.get('symbol') || '').trim().toUpperCase();
    return symbol;
}

async function loadDetail() {
    const symbol = getSymbolFromQuery();
    if (!symbol) {
        alert('Symbol parametresi yok. Örn: /detail.html?symbol=AKBNK');
        return;
    }

    showLoading(true);
    titleEl.textContent = `📌 ${symbol} Detay`;

    try {
        // Sadece API datası kullanıyoruz
        const [stockResp, historyResp] = await Promise.all([
            fetch(`${API_BASE_URL}/api/bist/stocks/${symbol}`),
            fetch(`${API_BASE_URL}/api/bist/history/${symbol}?days=60`)
        ]);

        if (!stockResp.ok || !historyResp.ok) {
            throw new Error('API yanıtı alınamadı');
        }

        const stock = await stockResp.json();
        const history = await historyResp.json();

        renderChart(symbol, history);
        renderCandlePercents(history);
    } catch (e) {
        console.error(e);
        candlesTableEl.innerHTML = '<div class="error">Detay verileri alınamadı. Önce /api/bist/update çağırmayı deneyin.</div>';
    } finally {
        showLoading(false);
    }
}

function showLoading(show) {
    loadingEl.classList.toggle('hidden', !show);
    refreshBtn.disabled = show;
}

function renderChart(symbol, history) {
    const sorted = history
        .map(h => ({ ...h, date: new Date(h.date) }))
        .sort((a, b) => a.date - b.date);

    const labels = sorted.map(h => h.date.toLocaleDateString('tr-TR', { month: 'short', day: 'numeric' }));
    const prices = sorted.map(h => h.price);

    if (chartInstance) chartInstance.destroy();

    const ctx = chartCanvas.getContext('2d');
    chartInstance = new Chart(ctx, {
        type: 'line',
        data: {
            labels,
            datasets: [{
                label: `${symbol} Kapanış`,
                data: prices,
                borderColor: '#667eea',
                backgroundColor: 'rgba(102,126,234,0.12)',
                tension: 0.35,
                pointRadius: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: true }
            }
        }
    });
}

function renderCandlePercents(history) {
    const sorted = history
        .map(h => ({ ...h, date: new Date(h.date) }))
        .sort((a, b) => a.date - b.date);

    if (sorted.length < 2) {
        candlesTableEl.innerHTML = '<div class="empty-state">Mum yüzdesi hesaplamak için yeterli veri yok.</div>';
        return;
    }

    // Günlük yüzde: (bugün - dün) / dün * 100
    const rows = [];
    for (let i = 1; i < sorted.length; i++) {
        const prev = sorted[i - 1];
        const cur = sorted[i];
        const pct = prev.price === 0 ? 0 : ((cur.price - prev.price) / prev.price) * 100;
        rows.push({
            date: cur.date,
            close: cur.price,
            pct
        });
    }

    // Son günler üstte
    rows.reverse();

    const html = `
        <div style="overflow:auto;">
            <table style="width:100%; border-collapse: collapse;">
                <thead>
                    <tr style="text-align:left; border-bottom: 2px solid #e9ecef;">
                        <th style="padding:10px;">Tarih</th>
                        <th style="padding:10px;">Kapanış</th>
                        <th style="padding:10px;">Günlük %</th>
                    </tr>
                </thead>
                <tbody>
                    ${rows.slice(0, 30).map(r => {
                        const cls = r.pct >= 0 ? 'positive' : 'negative';
                        const sign = r.pct >= 0 ? '+' : '';
                        return `
                            <tr style="border-bottom: 1px solid #f1f3f5;">
                                <td style="padding:10px;">${r.date.toLocaleDateString('tr-TR')}</td>
                                <td style="padding:10px;">₺${r.close.toFixed(2)}</td>
                                <td style="padding:10px;" class="stock-change ${cls}">${sign}${r.pct.toFixed(2)}%</td>
                            </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
        </div>
    `;

    candlesTableEl.innerHTML = html;
}

