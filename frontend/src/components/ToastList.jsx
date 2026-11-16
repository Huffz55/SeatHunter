export function ToastList({ toasts, onDismiss }) {
  return (
    <div className="fixed bottom-6 right-6 z-[100] flex flex-col gap-2.5 pointer-events-none max-w-sm w-full">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className="toast-slide pointer-events-auto flex items-center gap-3 bg-[#0f172a]/95 border border-white/[0.12] px-4 py-3 rounded-lg text-xs shadow-2xl backdrop-blur-xl"
        >
          <div
            className={`w-2 h-2 rounded-full shrink-0 ${
              toast.type === 'error'
                ? 'bg-rose-500'
                : toast.type === 'info'
                ? 'bg-indigo-400'
                : 'bg-emerald-400'
            }`}
          />
          <div className="flex-1">
            <div className="font-semibold text-slate-200">{toast.title}</div>
            <div className="text-slate-400 text-[11px] mt-0.5 font-normal">{toast.message}</div>
          </div>
          {onDismiss && (
            <button
              onClick={() => onDismiss(toast.id)}
              className="text-slate-500 hover:text-white transition-colors p-1"
            >
              <i className="fa-solid fa-xmark text-xs" />
            </button>
          )}
        </div>
      ))}
    </div>
  );
}
