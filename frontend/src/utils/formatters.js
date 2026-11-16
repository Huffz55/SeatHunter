export const getDomainLabel = (url) => {
  try {
    if (!url || !url.startsWith('http')) return url || 'Web';
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return 'Web';
  }
};

export const formatNotifTime = (isoDate, lang) => {
  if (!isoDate) return "";
  const d = new Date(isoDate);
  const diffSec = Math.floor((Date.now() - d.getTime()) / 1000);
  if (diffSec < 60) return lang === 'TR' ? 'Az önce' : 'Just now';
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return lang === 'TR' ? `${diffMin} dk önce` : `${diffMin}m ago`;
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return lang === 'TR' ? `${diffHr} sa önce` : `${diffHr}h ago`;
  return (
    d.toLocaleDateString(lang === 'TR' ? 'tr-TR' : 'en-US', { day: '2-digit', month: '2-digit' }) +
    ' ' +
    d.toLocaleTimeString(lang === 'TR' ? 'tr-TR' : 'en-US', { hour: '2-digit', minute: '2-digit' })
  );
};

export const localizeNotif = (n, lang) => {
  if (lang === 'TR' || !n) return { title: n?.title || '', message: n?.message || '' };

  const titleMap = {
    "Tarama Başlatıldı": "Hunt Started",
    "Tarama İptal Edildi": "Hunt Cancelled",
    "Bilet Bulundu!": "Ticket Found!",
    "Fiyat Düşüşü": "Price Drop",
    "Abonelik Süresi Doldu": "Subscription Expired"
  };

  let title = titleMap[n.title] || n.title || "";
  if (title.endsWith(" Planı Aktifleştirildi")) {
    title = title.replace(" Planı Aktifleştirildi", " Plan Activated");
  }

  let msg = n.message || "";
  const startMatch = msg.match(/^"(.+?)" için gerçek zamanlı bilet takibi başlatıldı\.$/);
  const cancelMatch = msg.match(/^"(.+?)" için aktif bilet takibi iptal edildi\.$/);
  const foundPriceMatch = msg.match(/^(.+?) için uygun bilet bulundu \(Fiyat: (.+?) TL\)\.$/);
  const foundMatch = msg.match(/^(.+?) için uygun bilet bulundu\.$/);
  const cartMatch = msg.match(/^(.+?) için koltuk otomatik olarak sepete eklendi\.$/);
  const dropMatch = msg.match(/^(.+?) bilet fiyatı (.+?) TL seviyesine düştü\.$/);

  if (startMatch) msg = `Real-time ticket tracking started for "${startMatch[1]}".`;
  else if (cancelMatch) msg = `Active ticket tracking cancelled for "${cancelMatch[1]}".`;
  else if (foundPriceMatch) msg = `Matching seats found for ${foundPriceMatch[1]} (Price: ${foundPriceMatch[2]} TL).`;
  else if (foundMatch) msg = `Matching seats found for ${foundMatch[1]}.`;
  else if (cartMatch) msg = `Seats automatically added to cart for ${cartMatch[1]}.`;
  else if (dropMatch) msg = `Ticket price for ${dropMatch[1]} dropped to ${dropMatch[2]} TL.`;

  return { title, message: msg };
};
