export function PriceAnalyticsModal({ modal, lang, t, onClose }) {
  if (!modal) return null;

  return (
    <div
      className="fixed inset-0 z-[70] flex items-center justify-center p-4 bg-black/75 backdrop-blur-md"
      onClick={onClose}
    >
      <div
        className="bg-[#0f172a] border border-white/[0.08] rounded-xl w-full max-w-lg shadow-2xl relative z-10 p-6 space-y-4 fade-enter"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h3 className="text-sm font-bold text-white truncate">{modal.title}</h3>
            <p className="text-[11px] font-mono text-slate-400 truncate mt-0.5">{modal.url}</p>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white p-1 rounded transition-colors"
          >
            <i className="fa-solid fa-xmark text-sm" />
          </button>
        </div>

        {modal.loading ? (
          <div className="py-8 text-center text-xs text-slate-400">
            <i className="fa-solid fa-circle-notch fa-spin mr-2 text-indigo-400" />
            {t('loadingUsers')}
          </div>
        ) : modal.notFound || !modal.summary ? (
          <div className="text-xs text-slate-400 bg-white/[0.02] p-4 rounded-lg border border-white/[0.05] text-center">
            {t('noPriceHistory')}
          </div>
        ) : (
          <div className="space-y-4">
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
              <div className="bg-[#070b14] p-3 rounded-lg border border-white/[0.05]">
                <div className="text-[10px] text-slate-400 uppercase">{t('currentPriceLabel')}</div>
                <div className="text-sm font-bold text-white mt-1">{modal.summary.currentPrice} TL</div>
              </div>
              <div className="bg-[#070b14] p-3 rounded-lg border border-white/[0.05]">
                <div className="text-[10px] text-slate-400 uppercase">{t('minPriceLabel')}</div>
                <div className="text-sm font-bold text-emerald-400 mt-1">{modal.summary.minPrice} TL</div>
              </div>
              <div className="bg-[#070b14] p-3 rounded-lg border border-white/[0.05]">
                <div className="text-[10px] text-slate-400 uppercase">{t('avgPriceLabel')}</div>
                <div className="text-sm font-bold text-indigo-300 mt-1">{modal.summary.averagePrice} TL</div>
              </div>
              <div className="bg-[#070b14] p-3 rounded-lg border border-white/[0.05]">
                <div className="text-[10px] text-slate-400 uppercase">{t('maxObservedPriceLabel')}</div>
                <div className="text-sm font-bold text-rose-400 mt-1">{modal.summary.maxPrice} TL</div>
              </div>
            </div>

            {modal.history && modal.history.length > 0 && (
              <div className="bg-[#070b14] border border-white/[0.05] rounded-lg p-3 space-y-2 max-h-44 overflow-y-auto">
                <div className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
                  {t('recentPriceHistory')}
                </div>
                {modal.history.slice(-8).reverse().map((snap, idx) => (
                  <div
                    key={idx}
                    className="flex items-center justify-between text-xs py-1 border-b border-white/[0.04] last:border-0"
                  >
                    <span className="text-slate-400 font-mono">
                      {new Date(snap.recordedAt || snap.observedAt || snap.timestamp || Date.now()).toLocaleString(
                        lang === 'TR' ? 'tr-TR' : 'en-US'
                      )}
                    </span>
                    <span className="font-semibold text-slate-200">
                      {snap.price} {snap.currency || 'TL'}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        <div className="flex justify-end pt-1">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg border border-white/[0.08] text-slate-300 hover:bg-white/[0.04] text-xs font-medium transition-colors"
          >
            {t('closeBtn')}
          </button>
        </div>
      </div>
    </div>
  );
}

export function DeleteAccountModal({ open, t, onClose, onConfirm }) {
  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-[70] flex items-center justify-center p-4 bg-black/75 backdrop-blur-md"
      onClick={onClose}
    >
      <div
        className="bg-[#0f172a] border border-white/[0.08] rounded-xl w-full max-w-sm shadow-2xl relative z-10 p-6 text-center fade-enter"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="text-sm font-bold text-white mb-2">{t('delConfirmTitle')}</h3>
        <p className="text-xs text-slate-400 mb-5 font-normal">{t('delConfirmMsg')}</p>
        <div className="flex gap-2.5">
          <button
            onClick={onClose}
            className="flex-1 py-2 rounded-lg border border-white/[0.08] text-slate-300 hover:bg-white/[0.04] transition-colors text-xs font-medium focus:outline-none"
          >
            {t('cancelBtn')}
          </button>
          <button
            onClick={onConfirm}
            className="flex-1 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 text-white transition-colors text-xs font-medium shadow-sm focus:outline-none"
          >
            {t('yesDelete')}
          </button>
        </div>
      </div>
    </div>
  );
}
