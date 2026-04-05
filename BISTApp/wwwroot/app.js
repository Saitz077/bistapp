// BIST Frontend Application
// API Base URL
const API_BASE_URL = window.location.origin;

// DOM Elements
const refreshBtn = document.getElementById('refreshBtn');
const loadingEl = document.getElementById('loading');
const filterContainers = {
    filter1: document.getElementById('filter1'),
    filter2: document.getElementById('filter2'),
    filter3: document.getElementById('filter3'),
    filter4: document.getElementById('filter4'),
    filter5: document.getElementById('filter5'),
    filter6: document.getElementById('filter6')
};

// Global Data Storage
let stocksData = [];
let historyData = {};

/**
 * Uygulama başlatıldığında verileri yükle
 */
document.addEventListener('DOMContentLoaded', () => {
    loadData();
    refreshBtn.addEventListener('click', loadData);
});

/**
 * API'den tüm verileri yükle
 */
async function loadData() {
    showLoading(true);
    
    try {
        // Paralel olarak stocks ve history verilerini çek
        const [stocksResponse, historyResponse] = await Promise.all([
            fetch(`${API_BASE_URL}/api/bist/stocks`),
            fetch(`${API_BASE_URL}/api/bist/history/all?days=10`)
        ]);

        if (!stocksResponse.ok || !historyResponse.ok) {
            throw new Error('API yanıtı alınamadı');
        }

        stocksData = await stocksResponse.json();
        historyData = await historyResponse.json();

        // Her filtreyi uygula ve göster
        applyFilters();
    } catch (error) {
        console.error('Veri yükleme hatası:', error);
        showError('Veriler yüklenirken bir hata oluştu. Lütfen tekrar deneyin.');
    } finally {
        showLoading(false);
    }
}

/**
 * Loading durumunu göster/gizle
 */
function showLoading(show) {
    loadingEl.classList.toggle('hidden', !show);
    refreshBtn.disabled = show;
}

/**
 * Hata mesajı göster
 */
function showError(message) {
    alert(message);
}

/**
 * Tüm filtreleri uygula ve sonuçları göster
 */
function applyFilters() {
    // Her filtre için ilgili hisseleri bul ve göster
    const filter1Stocks = filter1_last5DaysAtLeast4Negative();
    const filter2Stocks = filter2_last3DaysAllPositive();
    const filter3Stocks = filter3_last2DaysUp8Percent();
    const filter4Stocks = filter4_last2DaysDown8Percent();
    const filter5Stocks = filter5_last3DaysAllNegative();
    const filter6Stocks = filter6_last4DaysAllPositive();

    displayStocks(filterContainers.filter1, filter1Stocks);
    displayStocks(filterContainers.filter2, filter2Stocks);
    displayStocks(filterContainers.filter3, filter3Stocks);
    displayStocks(filterContainers.filter4, filter4Stocks);
    displayStocks(filterContainers.filter5, filter5Stocks);
    displayStocks(filterContainers.filter6, filter6Stocks);
}

/**
 * FILTRE 1: Son 5 işlem gününde en az 4 günü ekside kapanan hisseler
 */
function filter1_last5DaysAtLeast4Negative() {
    return stocksData
        .map(stock => {
            const history = getStockHistory(stock.symbol, 5);
            if (history.length < 5) return null;

            // Son 5 günün kapanış fiyatlarını hesapla
            const closes = history.slice(-5).map(h => h.price);
            let negativeDays = 0;

            // Her günü bir önceki günle karşılaştır
            for (let i = 1; i < closes.length; i++) {
                if (closes[i] < closes[i - 1]) {
                    negativeDays++;
                }
            }

            if (negativeDays >= 4) {
                const currentStock = stocksData.find(s => s.symbol === stock.symbol);
                return {
                    stock: currentStock || stock,
                    days: negativeDays,
                    trend: 'negative'
                };
            }
            return null;
        })
        .filter(item => item !== null);
}

/**
 * FILTRE 2: Son 3 günde 3 gün üst üste artıda kapanan hisseler (Momentum)
 */
function filter2_last3DaysAllPositive() {
    return stocksData
        .map(stock => {
            const history = getStockHistory(stock.symbol, 3);
            if (history.length < 3) return null;

            const closes = history.slice(-3).map(h => h.price);
            
            // Son 3 günün hepsi artışta mı?
            let allPositive = true;
            for (let i = 1; i < closes.length; i++) {
                if (closes[i] <= closes[i - 1]) {
                    allPositive = false;
                    break;
                }
            }

            if (allPositive) {
                const currentStock = stocksData.find(s => s.symbol === stock.symbol);
                return {
                    stock: currentStock || stock,
                    days: 3,
                    trend: 'positive'
                };
            }
            return null;
        })
        .filter(item => item !== null);
}

/**
 * FILTRE 3: Son 2 günde %8 veya üzeri yükselen hisseler
 */
function filter3_last2DaysUp8Percent() {
    return stocksData
        .map(stock => {
            const history = getStockHistory(stock.symbol, 2);
            if (history.length < 2) return null;

            const closes = history.slice(-2).map(h => h.price);
            const firstDay = closes[0];
            const lastDay = closes[closes.length - 1];
            
            const percentChange = ((lastDay - firstDay) / firstDay) * 100;

            if (percentChange >= 8) {
                const currentStock = stocksData.find(s => s.symbol === stock.symbol);
                return {
                    stock: currentStock || stock,
                    percentChange: percentChange,
                    trend: 'positive'
                };
            }
            return null;
        })
        .filter(item => item !== null)
        .sort((a, b) => b.percentChange - a.percentChange);
}

/**
 * FILTRE 4: Son 2 günde %8 veya üzeri düşen hisseler
 */
function filter4_last2DaysDown8Percent() {
    return stocksData
        .map(stock => {
            const history = getStockHistory(stock.symbol, 2);
            if (history.length < 2) return null;

            const closes = history.slice(-2).map(h => h.price);
            const firstDay = closes[0];
            const lastDay = closes[closes.length - 1];
            
            const percentChange = ((lastDay - firstDay) / firstDay) * 100;

            if (percentChange <= -8) {
                const currentStock = stocksData.find(s => s.symbol === stock.symbol);
                return {
                    stock: currentStock || stock,
                    percentChange: Math.abs(percentChange),
                    trend: 'negative'
                };
            }
            return null;
        })
        .filter(item => item !== null)
        .sort((a, b) => b.percentChange - a.percentChange);
}

/**
 * FILTRE 5: Son 3 günde 3 gün üst üste ekside kapanan hisseler
 */
function filter5_last3DaysAllNegative() {
    return stocksData
        .map(stock => {
            const history = getStockHistory(stock.symbol, 3);
            if (history.length < 3) return null;

            const closes = history.slice(-3).map(h => h.price);
            
            // Son 3 günün hepsi düşüşte mi?
            let allNegative = true;
            for (let i = 1; i < closes.length; i++) {
                if (closes[i] >= closes[i - 1]) {
                    allNegative = false;
                    break;
                }
            }

            if (allNegative) {
                const currentStock = stocksData.find(s => s.symbol === stock.symbol);
                return {
                    stock: currentStock || stock,
                    days: 3,
                    trend: 'negative'
                };
            }
            return null;
        })
        .filter(item => item !== null);
}

/**
 * FILTRE 6: Son 4 günde 4 gün üst üste artıda kapanan hisseler
 */
function filter6_last4DaysAllPositive() {
    return stocksData
        .map(stock => {
            const history = getStockHistory(stock.symbol, 4);
            if (history.length < 4) return null;

            const closes = history.slice(-4).map(h => h.price);
            
            // Son 4 günün hepsi artışta mı?
            let allPositive = true;
            for (let i = 1; i < closes.length; i++) {
                if (closes[i] <= closes[i - 1]) {
                    allPositive = false;
                    break;
                }
            }

            if (allPositive) {
                const currentStock = stocksData.find(s => s.symbol === stock.symbol);
                return {
                    stock: currentStock || stock,
                    days: 4,
                    trend: 'positive'
                };
            }
            return null;
        })
        .filter(item => item !== null);
}

/**
 * Belirli bir hissenin geçmiş verilerini getir
 */
function getStockHistory(symbol, days) {
    const stockHistory = historyData[symbol] || [];
    
    // Tarihe göre sırala ve son N günü al
    const sorted = stockHistory
        .map(h => ({
            ...h,
            date: new Date(h.date)
        }))
        .sort((a, b) => a.date - b.date);

    return sorted.slice(-days);
}

/**
 * Hisseleri DOM'da göster
 */
function displayStocks(container, stocks) {
    if (!container) return;

    if (stocks.length === 0) {
        container.innerHTML = '<div class="empty-state">Bu kritere uyan hisse bulunamadı</div>';
        return;
    }

    container.innerHTML = stocks.map(item => {
        const stock = item.stock;
        const changeClass = stock.change >= 0 ? 'positive' : 'negative';
        const changeSign = stock.change >= 0 ? '+' : '';
        
        let daysInfo = '';
        if (item.days) {
            daysInfo = `${item.days} gündür ${item.trend === 'positive' ? 'artıda' : 'ekside'}`;
        } else if (item.percentChange) {
            daysInfo = `2 günde %${item.percentChange.toFixed(2)} ${item.trend === 'positive' ? 'yükseliş' : 'düşüş'}`;
        }

        return `
            <div class="stock-card">
                <div class="stock-symbol">${stock.symbol}</div>
                <div class="stock-price">₺${stock.price.toFixed(2)}</div>
                <div class="stock-change ${changeClass}">
                    ${changeSign}${stock.change.toFixed(2)}%
                </div>
                <div class="stock-days">${daysInfo}</div>
            </div>
        `;
    }).join('');
}
