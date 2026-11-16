export function SignalRBanner({ status, onReconnect, t }) {
  if (!status || status === 'connected') return null;

  const isReconnecting = status === 'reconnecting';

  return (
    <div
      className={`mx-6 lg:mx-8 mt-4 px-4 py-2.5 rounded-xl border flex items-center justify-between gap-3 text-xs fade-enter ${
        isReconnecting
          ? 'bg-amber-500/10 border-amber-500/25 text-amber-200'
          : 'bg-rose-500/10 border-rose-500/25 text-rose-200'
      }`}
    >
      <div className="flex items-center gap-2.5 min-w-0">
        <span
          className={`w-2 h-2 rounded-full shrink-0 ${
            isReconnecting ? 'bg-amber-400 animate-pulse' : 'bg-rose-400'
          }`}
        />
        <span className="truncate font-medium">
          {isReconnecting ? t('signalrReconnecting') : t('signalrDisconnected')}
        </span>
      </div>
      {!isReconnecting && onReconnect && (
        <button
          onClick={onReconnect}
          className="px-3 py-1 rounded-lg bg-rose-500/20 hover:bg-rose-500/30 border border-rose-500/30 text-rose-100 text-[11px] font-semibold transition-colors shrink-0 cursor-pointer"
        >
          <i className="fa-solid fa-rotate-right mr-1.5" />
          {t('reconnectBtn')}
        </button>
      )}
    </div>
  );
}
