// BIST Stocks List Page
const API_BASE_URL = window.location.origin;

const refreshBtn = document.getElementById('refreshBtn');
const loadingEl = document.getElementById('loading');
const stocksGrid = document.getElementById('stocksGrid');

document.addEventListener('DOMContentLoaded', () => {
    loadStocks();
    refreshBtn.addEventListener('click', loadStocks);
});

async function loadStocks() {
    showLoading(true);
    
    try {
        const [stocksResponse, historyResponse] = await Promise.all([
            fetch(`${API_BASE_URL}/api/bist/stocks`),
            fetch(`${API_BASE_URL}/api/bist/history/all?days=30`)
        ]);

        if (!stocksResponse.ok || !historyResponse.ok) {
            throw new Error('API yanıtı alınamadı');
        }

        const stocks = await stocksResponse.json();
        const history = await historyResponse.json();

        displayStocks(stocks, history);
    } catch (error) {
        console.error('Veri yükleme hatası:', error);
        stocksGrid.innerHTML = '<div class="error">Veriler yüklenirken bir hata oluştu.</div>';
    } finally {
        showLoading(false);
    }
}

function showLoading(show) {
    loadingEl.classList.toggle('hidden', !show);
    refreshBtn.disabled = show;
}

function displayStocks(stocks, history) {
    if (stocks.length === 0) {
        stocksGrid.innerHTML = '<div class="empty-state">Henüz hisse verisi bulunmuyor.</div>';
        return;
    }

    stocksGrid.innerHTML = stocks.map(stock => {
        const stockHistory = history[stock.symbol] || [];
        const changeClass = stock.change >= 0 ? 'positive' : 'negative';
        const changeSign = stock.change >= 0 ? '+' : '';
        const historyCount = stockHistory.length;

        return `
            <div class="stock-card-large" onclick="showStockDetail('${stock.symbol}')">
                <div class="stock-header">
                    <div class="stock-symbol">${stock.symbol}</div>
                    <div class="stock-price">₺${stock.price.toFixed(2)}</div>
                </div>
                <div class="stock-change ${changeClass}">
                    ${changeSign}${stock.change.toFixed(2)}%
                </div>
                <div class="stock-info">
                    <div>Güncelleme: ${new Date(stock.updatedAt).toLocaleDateString('tr-TR')}</div>
                    <div>Geçmiş Veri: ${historyCount} gün</div>
                </div>
                ${historyCount > 0 ? `<canvas id="chart-${stock.symbol}" class="mini-chart"></canvas>` : ''}
            </div>
        `;
    }).join('');

    // Mini grafikleri çiz
    stocks.forEach(stock => {
        const stockHistory = history[stock.symbol];
        if (stockHistory && stockHistory.length > 0) {
            drawMiniChart(`chart-${stock.symbol}`, stockHistory, stock.symbol);
        }
    });
}

function drawMiniChart(canvasId, history, symbol) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const sorted = history
        .map(h => ({ ...h, date: new Date(h.date) }))
        .sort((a, b) => a.date - b.date)
        .slice(-10); // Son 10 gün

    const ctx = canvas.getContext('2d');
    new Chart(ctx, {
        type: 'line',
        data: {
            labels: sorted.map(h => h.date.toLocaleDateString('tr-TR', { month: 'short', day: 'numeric' })),
            datasets: [{
                label: symbol,
                data: sorted.map(h => h.price),
                borderColor: '#667eea',
                backgroundColor: 'rgba(102, 126, 234, 0.1)',
                tension: 0.4,
                pointRadius: 2,
                pointHoverRadius: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false }
            },
            scales: {
                y: {
                    display: false
                },
                x: {
                    display: false
                }
            },
            interaction: {
                intersect: false
            }
        }
    });
}

function showStockDetail(symbol) {
    window.location.href = `/detail.html?symbol=${symbol}`;
}
